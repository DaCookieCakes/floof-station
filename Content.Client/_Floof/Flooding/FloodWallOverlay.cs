using System.Numerics;
using Content.Client._Floof.Ocean;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Ocean;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Flooding;

[Virtual]
public partial class FloodWallOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;

    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> FillShader = "FloodFill";
    private static readonly ProtoId<ShaderPrototype> ObjectShader = "FloodObject";

    private const float PixelSize = 1f / 32f;

    private const int TilePixels = 32;

    private const int SouthFaceRows = 13;
    private const int NorthFaceRows = 5;
    private const int SideFaceRows = 9;

    private static readonly Vector2i[] Directions = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

    private readonly FloodBarrier _pass;
    private readonly FloodWaterlineSystem _system;
    private readonly FloodSubmergedSystem _submerged;
    private readonly MapFluidSystem _mapFluid;
    private readonly MapFluidVisualsSystem _fluidVisuals;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly SpriteSystem _sprite;
    private readonly SpriteTreeSystem _spriteTree;
    private readonly ShaderInstance _fill;
    private readonly ShaderInstance _above;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly HashSet<Entity<SpriteComponent, TransformComponent>> _sprites = new();
    private readonly FloodWaterlineSystem.WetTile _sea = new() { Depth = 1f };

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public FloodWallOverlay() : this(FloodBarrier.Wall, (int) DrawDepth.Objects + 1)
    {
    }

    protected FloodWallOverlay(FloodBarrier pass, int zIndex)
    {
        _pass = pass;
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWaterlineSystem>();
        _submerged = _entManager.System<FloodSubmergedSystem>();
        _mapFluid = _entManager.System<MapFluidSystem>();
        _fluidVisuals = _entManager.System<MapFluidVisualsSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _spriteTree = _entManager.System<SpriteTreeSystem>();
        _fill = _proto.Index(FillShader).InstanceUnique();
        _above = _proto.Index(ObjectShader).InstanceUnique();

        ZIndex = zIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var underwater = _submerged.SubmergedView > 0f;

        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _system.NextStencilRef(),
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };

        if (underwater)
        {
            _fill.Stencil = stencil;
            handle.UseShader(_fill);
        }

        var sea = _fluidVisuals.TryGetFluidColor(args.MapId, out _sea.Color);

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        foreach (var grid in _grids)
        {
            _system.Grids.TryGetValue(grid, out var tiles);
            if (!sea && (tiles == null || tiles.Count == 0))
                continue;

            var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
            handle.SetTransform(matrix);
            var bounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(2f);

            if (tiles != null)
            {
                foreach (var (indices, tile) in tiles)
                {
                    if (tile.Depth > 0f && bounds.Contains(indices))
                        DrawBandsAround(handle, grid, indices, tile);
                }
            }

            if (sea)
                DrawSeaBands(handle, grid, bounds);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);

        if (underwater && args.Viewport.Eye is { } eye)
            DarkenAboveWater(args, stencil, eye.Rotation);
    }

    private void DrawSeaBands(DrawingHandleWorld handle, Entity<MapGridComponent> grid, Box2 bounds)
    {
        var area = bounds.Intersect(grid.Comp.LocalAABB.Enlarged(1f));
        for (var x = (int) MathF.Floor(area.Left); x < MathF.Ceiling(area.Right); x++)
        {
            for (var y = (int) MathF.Floor(area.Bottom); y < MathF.Ceiling(area.Top); y++)
            {
                var indices = new Vector2i(x, y);
                if (_mapFluid.IsExposed(grid, grid, indices) && GetBarrier(grid, indices) == FloodBarrier.None)
                    DrawBandsAround(handle, grid, indices, _sea);
            }
        }
    }

    private void DrawBandsAround(DrawingHandleWorld handle, Entity<MapGridComponent> grid, Vector2i indices, FloodWaterlineSystem.WetTile tile)
    {
        for (var i = 0; i < Directions.Length; i++)
        {
            var dir = Directions[i];
            var next = Directions[(i + 1) % Directions.Length];
            var wall = indices + dir;
            if (GetBarrier(grid, wall) == _pass)
                DrawBand(handle, grid, wall, dir, tile);

            if (GetBarrier(grid, wall + next) == _pass &&
                GetBarrier(grid, wall) != FloodBarrier.None &&
                GetBarrier(grid, indices + next) != FloodBarrier.None)
            {
                DrawInnerCorner(handle, wall + next, dir, next, tile);
            }
        }
    }

    private void DarkenAboveWater(in OverlayDrawArgs args, StencilParameters stencil, Angle eyeRotation)
    {
        var handle = args.WorldHandle;

        _above.Stencil = stencil;
        _above.SetParameter("waterline", -1f);
        _above.SetParameter("line_size", 0f);
        _above.SetParameter("water_color", Color.Transparent);
        _above.SetParameter("line_color", Color.Transparent);
        _above.SetParameter("above_color", _submerged.AboveColor);
        handle.UseShader(_above);

        _sprites.Clear();
        _spriteTree.QueryAabb(_sprites, args.MapId, args.WorldBounds);

        foreach (var ent in _sprites)
        {
            var (uid, sprite, xform) = ent;
            if (!sprite.Visible || _system.GetBarrier(uid) != _pass)
                continue;

            var (position, rotation) = _xform.GetWorldPositionRotation(xform);
            _sprite.RenderSprite((uid, sprite), handle, eyeRotation, rotation, position);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawBand(DrawingHandleWorld handle, Entity<MapGridComponent> grid, Vector2i wall, Vector2i dir, FloodWaterlineSystem.WetTile tile)
    {
        var side = new Vector2i(dir.Y, -dir.X);
        var freeA = GetBarrier(grid, wall + side) == FloodBarrier.None;
        var freeB = GetBarrier(grid, wall - side) == FloodBarrier.None;

        var faceRows = FaceRows(dir);
        var rows = ShownRows(tile, dir);
        for (var row = 0; row < rows; row++)
        {
            var insetA = freeA ? row * FaceRows(-side) / faceRows : 0;
            var insetB = freeB ? row * FaceRows(side) / faceRows : 0;
            handle.DrawRect(RowBox(wall, dir, row, insetA, insetB), RowColor(tile.Color, row == rows - 1));
        }
    }

    private static void DrawInnerCorner(DrawingHandleWorld handle, Vector2i wall, Vector2i first, Vector2i second, FloodWaterlineSystem.WetTile tile)
    {
        var firstRows = ShownRows(tile, first);
        var secondRows = ShownRows(tile, second);

        for (var row = 0; row < firstRows; row++)
        {
            var box = RowBox(wall, first, row, TilePixels - secondRows + 1, 0);
            handle.DrawRect(box, RowColor(tile.Color, row == firstRows - 1));
        }

        handle.DrawRect(RowBox(wall, second, secondRows - 1, 0, TilePixels - firstRows), RowColor(tile.Color, true));
    }

    private static int FaceRows(Vector2i dir)
    {
        return (dir.X, dir.Y) switch
        {
            (0, 1) => SouthFaceRows,
            (0, -1) => NorthFaceRows,
            _ => SideFaceRows,
        };
    }

    private static int ShownRows(FloodWaterlineSystem.WetTile tile, Vector2i dir)
    {
        var faceRows = FaceRows(dir);
        return Math.Clamp((int) MathF.Round(tile.Depth * faceRows), 1, faceRows);
    }

    private static Color RowColor(Color color, bool waterline)
    {
        return waterline ? FloodWaterlineSystem.WaterlineColor(color) : color;
    }

    private static Box2 RowBox(Vector2i wall, Vector2i dir, int row, int insetA, int insetB)
    {
        var near = row * PixelSize;
        var far = near + PixelSize;
        var a = insetA * PixelSize;
        var b = insetB * PixelSize;
        var (x, y) = (wall.X, wall.Y);

        return (dir.X, dir.Y) switch
        {
            (0, 1) => new Box2(x + b, y + near, x + 1 - a, y + far),
            (0, -1) => new Box2(x + a, y + 1 - far, x + 1 - b, y + 1 - near),
            (1, 0) => new Box2(x + near, y + a, x + far, y + 1 - b),
            _ => new Box2(x + 1 - far, y + b, x + 1 - near, y + 1 - a),
        };
    }

    private FloodBarrier GetBarrier(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var result = FloodBarrier.None;
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (ent is not { } uid || _entManager.HasComponent<FloodDryComponent>(uid))
                continue;

            var barrier = _system.GetBarrier(uid);
            if (barrier == FloodBarrier.Wall)
                return barrier;

            if (barrier == FloodBarrier.Door)
                result = barrier;
        }

        return result;
    }
}

public sealed partial class FloodDoorOverlay : FloodWallOverlay
{
    public FloodDoorOverlay() : base(FloodBarrier.Door, (int) DrawDepth.Overdoors + 1)
    {
    }
}
