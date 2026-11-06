using System.Numerics;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Floof.AnimatedTiles;

/// <summary>
/// Tiles can't animate, so this draws the animations over them. On a planet map the ground is hidden under
/// floating grids with the stencil.
/// </summary>
public sealed partial class AnimatedTileOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const int MinStencilRef = 2;
    private const int MaxStencilRef = 254;
    private static readonly ProtoId<ShaderPrototype> Shader = "AnimatedTile";

    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly TurfSystem _turf;
    private readonly AnimatedTileSystem _animated;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _fill;
    private readonly ShaderInstance _redraw;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly Dictionary<Texture, List<Box2>> _fills = new();
    private int _stencilRef = MinStencilRef;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public AnimatedTileOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _turf = _entManager.System<TurfSystem>();
        _animated = _entManager.System<AnimatedTileSystem>();
        _mask = _proto.Index(Shader).InstanceUnique();
        _fill = _proto.Index(Shader).InstanceUnique();
        _redraw = _proto.Index(Shader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var mapUid = _map.GetMapOrInvalid(args.MapId);

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        if (_entManager.TryGetComponent<MapGridComponent>(mapUid, out var ground))
            DrawGround(args, (mapUid, ground));

        foreach (var grid in _grids)
        {
            if (grid.Owner != mapUid)
                DrawGrid(args, grid, null);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawGround(in OverlayDrawArgs args, Entity<MapGridComponent> ground)
    {
        // A new ref each draw, so marks left in the stencil from earlier frames don't count.
        _stencilRef = _stencilRef >= MaxStencilRef ? MinStencilRef : _stencilRef + 1;
        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _stencilRef,
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        _mask.Stencil = stencil;
        _fill.Stencil = stencil;

        // Only where the ground was drawn, which leaves the ref behind.
        _redraw.Stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _stencilRef,
            Op = StencilOp.Keep,
            Func = StencilFunc.Equal,
        };

        // Solid floors of floating grids mark the stencil, so the ground stays off them.
        var handle = args.WorldHandle;
        var seeThrough = _animated.SeeThrough;
        handle.UseShader(_mask);
        foreach (var grid in _grids)
        {
            if (grid.Owner == ground.Owner)
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (!_turf.IsSpace(tile) && seeThrough[tile.Tile.TypeId] == null)
                    handle.DrawRect(new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One), Color.Transparent);
            }
        }

        if (DrawGrid(args, ground, _fill))
            DrawSeeThrough(args, ground);
    }

    /// <summary>
    /// Draws one grid's animated tiles. Returns whether anything was drawn.
    /// </summary>
    private bool DrawGrid(in OverlayDrawArgs args, Entity<MapGridComponent> grid, ShaderInstance? shader)
    {
        var time = _timing.RealTime;
        var tiles = _animated.Tiles;
        var drawn = false;

        foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
        {
            if (tiles[tile.Tile.TypeId] is not { } animation)
                continue;

            Add(animation.GetFrame(time), new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One));
            drawn = true;
        }

        var handle = args.WorldHandle;
        handle.UseShader(shader);
        handle.SetTransform(_xform.GetWorldMatrix(grid));
        Flush(handle);
        return drawn;
    }

    private void Add(Texture texture, Box2 box)
    {
        if (!_fills.TryGetValue(texture, out var boxes))
        {
            boxes = new List<Box2>();
            _fills[texture] = boxes;
        }

        boxes.Add(box);
    }

    // Drawn grouped by texture so the quads batch.
    private void Flush(DrawingHandleWorld handle)
    {
        foreach (var (texture, boxes) in _fills)
        {
            foreach (var box in boxes)
            {
                handle.DrawTextureRect(texture, box);
            }

            boxes.Clear();
        }
    }

    /// <summary>
    /// See-through tiles (lattice, glass) aren't masked so the ground shows through them, but it covers them too.
    /// This draws them again on top.
    /// </summary>
    private void DrawSeeThrough(in OverlayDrawArgs args, Entity<MapGridComponent> ground)
    {
        var handle = args.WorldHandle;
        var seeThrough = _animated.SeeThrough;
        handle.UseShader(_redraw);

        foreach (var grid in _grids)
        {
            if (grid.Owner == ground.Owner)
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (seeThrough[tile.Tile.TypeId] is not { } texture)
                    continue;

                // Variants sit side by side in the sprite, each as wide as it is tall.
                var tileSize = texture.Height;
                var left = tile.Tile.Variant * tileSize;
                handle.DrawTextureRectRegion(texture,
                    new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One),
                    null,
                    new UIBox2(left, 0, left + tileSize, tileSize));
            }
        }
    }
}
