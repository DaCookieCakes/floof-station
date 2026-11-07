using System.Numerics;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Swimming;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Draws the part of a sprite below the waterline again as water. A pass runs right after the sprites it covers,
///     anything drawn later stands in front of that water.
/// </summary>
[Virtual]
public partial class FloodObjectOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodObject";
    private static readonly ProtoId<ShaderPrototype> MaskShader = "FloodMask";

    private const int PixelsPerTile = 32;

    /// <summary>
    ///     Pixels the water climbs up a sprite in a full tile. A wall squeezes its height into a short face,
    ///     objects don't, so the same water reaches much further up them.
    /// </summary>
    private const int MaxRows = PixelsPerTile;

    /// <summary>
    ///     A sprite lying sideways sits in the middle of its tile, clear of shallow water. This wets its lower half.
    /// </summary>
    private const int LyingSwimRows = 15;

    /// <summary>
    ///     Matches what painted masks use, so the sprite still shows through.
    /// </summary>
    private const float FlatSolidity = 0.75f;

    private readonly int _minDepth;
    private readonly int _maxDepth;
    private readonly FloodWaterlineSystem _system;
    private readonly FloodSubmergedSystem _submerged;
    private readonly FloodLevelSystem _level;
    private readonly SwimmingSystem _swimming;
    private readonly SpriteSystem _sprite;
    private readonly SpriteTreeSystem _spriteTree;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly EntityQuery<MapGridComponent> _gridQuery;
    private readonly EntityQuery<FloodMaskComponent> _maskQuery;
    private readonly EntityQuery<FloodDryComponent> _dryQuery;

    private readonly HashSet<Entity<SpriteComponent, TransformComponent>> _sprites = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), float> _surfaces = new();

    // A draw only reads its shader's parameters when the batch is flushed, so every sprite needs its own instance.
    private readonly List<ShaderInstance> _shaders = new();
    private readonly List<ShaderInstance> _maskShaders = new();
    private ShaderInstance? _dryShader;
    private readonly List<(SpriteComponent.Layer Layer, ShaderInstance Shader)> _layerShaders = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public FloodObjectOverlay() : this(DrawDepth.HighFloorObjects, DrawDepth.LargeObjects)
    {
    }

    protected FloodObjectOverlay(DrawDepth minDepth, DrawDepth maxDepth)
    {
        IoCManager.InjectDependencies(this);
        _minDepth = (int) minDepth;
        _maxDepth = (int) maxDepth;
        _system = _entManager.System<FloodWaterlineSystem>();
        _submerged = _entManager.System<FloodSubmergedSystem>();
        _level = _entManager.System<FloodLevelSystem>();
        _swimming = _entManager.System<SwimmingSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _spriteTree = _entManager.System<SpriteTreeSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _gridQuery = _entManager.GetEntityQuery<MapGridComponent>();
        _maskQuery = _entManager.GetEntityQuery<FloodMaskComponent>();
        _dryQuery = _entManager.GetEntityQuery<FloodDryComponent>();

        ZIndex = _maxDepth + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var underwater = _submerged.SubmergedView > 0f;
        if (_system.Grids.Count == 0 && !underwater || args.Viewport.Eye is not { } eye)
            return;

        var handle = args.WorldHandle;
        var scale = (args.Viewport.WorldToLocal(eye.Position.Position + Vector2.UnitX) -
                     args.Viewport.WorldToLocal(eye.Position.Position)).Length();
        var pixel = scale / PixelsPerTile;

        _sprites.Clear();
        _spriteTree.QueryAabb(_sprites, args.MapId, args.WorldBounds);
        _surfaces.Clear();

        var used = 0;
        var usedMasks = 0;
        var dryReady = false;

        // A pixel takes the first coat that covers it and refuses the rest, so overlapping sprites stay one coat.
        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _system.NextStencilRef(),
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };

        foreach (var ent in _sprites)
        {
            var (uid, sprite, xform) = ent;
            if (sprite.DrawDepth < _minDepth || sprite.DrawDepth > _maxDepth || !sprite.Visible)
                continue;

            if (_level.IsIntangible(uid))
                continue;

            FloodWaterlineSystem.WetTile? tile = null;
            var surface = 0f;
            if (!_dryQuery.HasComp(uid) &&
                xform.GridUid is { } gridUid &&
                _system.Grids.TryGetValue(gridUid, out var tiles) &&
                _gridQuery.TryComp(gridUid, out var grid) &&
                tiles.TryGetValue(_map.TileIndicesFor(gridUid, grid, xform.Coordinates), out var wet) &&
                wet.Depth > 0f)
            {
                if (wet.Depth < _swimming.DrownDepth && _level.RestsOnSurface(uid, xform))
                    surface = GetSurface(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));

                if (wet.Depth > surface)
                    tile = wet;
            }

            if (tile == null && !underwater)
                continue;

            // Seen from below, walls and doors are darkened by their own overlay.
            if (underwater && _system.GetBarrier(uid) != FloodBarrier.None)
                continue;

            var (position, rotation) = _xform.GetWorldPositionRotation(xform);

            if (tile == null)
            {
                // All one coat, so these can share a shader and batch.
                if (!dryReady)
                {
                    PrepareDryShader(stencil);
                    dryReady = true;
                }

                RenderCovered(handle, _dryShader!, (uid, sprite), eye.Rotation, rotation, position);
                continue;
            }

            var waterColor = tile.Color;
            var shown = tile.Depth - surface;

            // Fully under there's no surface to see.
            var covered = tile.Depth >= _swimming.DrownDepth;
            var lineColor = covered ? waterColor : FloodWaterlineSystem.WaterlineColor(waterColor);

            _maskQuery.TryComp(uid, out var mask);
            var flat = mask is { Sprite: null };

            if (mask?.Sprite is { } maskSprite)
            {
                if (usedMasks == _maskShaders.Count)
                    _maskShaders.Add(_proto.Index(MaskShader).InstanceUnique());

                var maskShader = _maskShaders[usedMasks++];
                maskShader.SetParameter("level", covered ? 1f : shown);
                maskShader.SetParameter("line_size", 1f / PixelsPerTile);
                maskShader.SetParameter("water_color", waterColor);
                maskShader.SetParameter("line_color", lineColor);
                maskShader.Stencil = stencil;

                handle.UseShader(maskShader);
                if (mask.Corners is { } corners && maskSprite is SpriteSpecifier.Rsi maskRsi)
                    DrawCorners(handle, sprite, maskRsi, corners, eye.Rotation, rotation, position);
                else
                    DrawMask(handle, sprite, LookOf(sprite, mask, maskSprite), eye.Rotation, rotation, position);

                continue;
            }

            var rows = Math.Clamp((int) MathF.Round(shown * MaxRows), 1, MaxRows);
            if (rows < LyingSwimRows && _swimming.IsSwimmingLying(uid, xform))
                rows = LyingSwimRows;

            var bottom = args.Viewport.WorldToLocal(position).Y + scale / 2f;

            if (used == _shaders.Count)
                _shaders.Add(_proto.Index(Shader).InstanceUnique());

            var shader = _shaders[used++];
            shader.SetParameter("waterline", covered || flat ? args.Viewport.Size.Y * 2f : args.Viewport.Size.Y - bottom + rows * pixel);
            shader.SetParameter("line_size", pixel);
            shader.SetParameter("water_color", flat ? waterColor.WithAlpha(waterColor.A * FlatSolidity) : waterColor);
            shader.SetParameter("line_color", lineColor);
            shader.SetParameter("above_color", _submerged.AboveColor);
            shader.Stencil = stencil;

            RenderCovered(handle, shader, (uid, sprite), eye.Rotation, rotation, position);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private float GetSurface(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        if (!_surfaces.TryGetValue((gridUid, tile), out var surface))
            _surfaces[(gridUid, tile)] = surface = _level.GetSurface(gridUid, grid, tile);

        return surface;
    }

    private void PrepareDryShader(StencilParameters stencil)
    {
        _dryShader ??= _proto.Index(Shader).InstanceUnique();
        _dryShader.SetParameter("waterline", -1f);
        _dryShader.SetParameter("line_size", 0f);
        _dryShader.SetParameter("water_color", Color.Transparent);
        _dryShader.SetParameter("line_color", Color.Transparent);
        _dryShader.SetParameter("above_color", _submerged.AboveColor);
        _dryShader.Stencil = stencil;
    }

    private static SpriteSpecifier LookOf(SpriteComponent sprite, FloodMaskComponent mask, SpriteSpecifier fallback)
    {
        if (mask.Looks.Count == 0 || fallback is not SpriteSpecifier.Rsi rsi)
            return fallback;

        foreach (var layer in sprite.AllLayers)
        {
            if (layer.Visible && layer.RsiState.Name is { } state && mask.Looks.TryGetValue(state, out var look))
                return new SpriteSpecifier.Rsi(rsi.RsiPath, look);
        }

        return fallback;
    }

    /// <summary>
    ///     Places the mask the way the sprite system places a layer, so it lines up whichever way the entity faces.
    /// </summary>
    private void DrawMask(DrawingHandleWorld handle, SpriteComponent sprite, SpriteSpecifier mask, Angle eyeRotation, Angle rotation, Vector2 position, SpriteComponent.DirectionOffset offset = SpriteComponent.DirectionOffset.None)
    {
        var angle = (rotation + eyeRotation).Reduced().FlipPositive();
        var state = _sprite.RsiStateLike(mask);
        var dir = SpriteComponent.Layer.GetDirection(state.RsiDirections, angle);
        var texture = state.GetFrame(dir.OffsetRsiDir(offset), 0);

        var cardinal = !sprite.NoRotation && sprite.SnapCardinals ? angle.RoundToCardinalAngle() : Angle.Zero;
        var world = Matrix3Helpers.CreateTransform(position, sprite.NoRotation ? -eyeRotation : rotation - cardinal);
        var matrix = Matrix3x2.Multiply(sprite.LocalMatrix, world);

        if (dir != RsiDirection.South && !sprite.NoRotation)
            matrix = Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-dir.Convert().ToAngle()), matrix);

        handle.SetTransform(matrix);
        handle.DrawTexture(texture, -(Vector2) texture.Size / PixelsPerTile / 2f);
    }

    /// <summary>
    ///     A smoothed sprite is four corner layers, each showing its own state. The mask has a piece for every one of them.
    /// </summary>
    private void DrawCorners(DrawingHandleWorld handle, SpriteComponent sprite, SpriteSpecifier.Rsi mask, string corners, Angle eyeRotation, Angle rotation, Vector2 position)
    {
        foreach (var layer in sprite.AllLayers)
        {
            if (layer.Visible && layer.RsiState.Name is { } state && state.StartsWith(corners))
                DrawMask(handle, sprite, new SpriteSpecifier.Rsi(mask.RsiPath, state), eyeRotation, rotation, position, layer.DirOffset);
        }
    }

    /// <summary>
    ///     A layer's own shader (displaced clothing on mobs) would replace ours, and clears ours for every layer after it.
    ///     So they're lifted off for this draw.
    /// </summary>
    private void RenderCovered(DrawingHandleWorld handle, ShaderInstance shader, Entity<SpriteComponent> sprite, Angle eyeRotation, Angle rotation, Vector2 position)
    {
        _layerShaders.Clear();
        foreach (var layer in sprite.Comp.AllLayers)
        {
            if (layer is SpriteComponent.Layer { Shader: { } own } shaded)
            {
                _layerShaders.Add((shaded, own));
                shaded.Shader = null;
            }
        }

        handle.UseShader(shader);
        _sprite.RenderSprite(sprite, handle, eyeRotation, rotation, position);

        foreach (var (layer, own) in _layerShaders)
        {
            layer.Shader = own;
        }
    }
}

public sealed partial class FloodItemOverlay : FloodObjectOverlay
{
    public FloodItemOverlay() : base(DrawDepth.Items, DrawDepth.BelowMobs)
    {
    }
}

public sealed partial class FloodMobOverlay : FloodObjectOverlay
{
    public FloodMobOverlay() : base(DrawDepth.Mobs, DrawDepth.OverMobs)
    {
    }
}
