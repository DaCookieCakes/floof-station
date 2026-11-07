using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Shared.Doors.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Tag;
using Content.Shared.Wall;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Flooding;

public enum FloodBarrier : byte
{
    None,
    Wall,
    Door,
}

/// <summary>
///     Tracks how deep the liquid on each wet tile is, for the overlays to draw.
/// </summary>
public sealed partial class FloodWaterlineSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    /// <summary>
    ///     Below this depth the liquid is a film that doesn't reach the wall. About 1 cm.
    /// </summary>
    private const float MinDepth = 0.06f;

    private const float WaterlineLighten = 0.35f;
    private const float WaterlineAlphaBoost = 0.15f;

    /// <summary>
    ///     Each stencil user marks with its own value. 0 is the cleared buffer (or 255), 1 is used by the engine's stencil shaders.
    /// </summary>
    private const int MinStencilRef = 2;
    private const int MaxStencilRef = 254;

    private int _stencilRef = MinStencilRef;

    public readonly Dictionary<EntityUid, Dictionary<Vector2i, WetTile>> Grids = new();

    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile)> _puddles = new();

    public sealed class WetTile
    {
        public float Depth;
        public Color Color;
        public float Opacity;

        /// <summary>
        ///     The current on this tile as last measured, and eased towards it for drawing. See <see cref="FloodFlowSystem"/>.
        /// </summary>
        public Vector2 FlowTarget;
        public Vector2 Flow;
        public float FlowPhase;

        /// <summary>
        ///     Seconds this tile has been far enough inside the local player's view for its neighbours to be known.
        /// </summary>
        public float InRange;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuddleComponent, ComponentShutdown>(OnPuddleShutdown);
    }

    public int NextStencilRef()
    {
        _stencilRef = _stencilRef >= MaxStencilRef ? MinStencilRef : _stencilRef + 1;
        return _stencilRef;
    }

    public FloodBarrier GetBarrier(EntityUid ent)
    {
        // Directional windows and windoors don't smooth and don't fill the tile.
        var smooths = HasComp<IconSmoothComponent>(ent);
        if (HasComp<WallComponent>(ent) || smooths && _tag.HasTag(ent, WindowTag))
            return FloodBarrier.Wall;

        if (smooths && TryComp<DoorComponent>(ent, out var door) &&
            door.State is not (DoorState.Open or DoorState.Opening))
        {
            return FloodBarrier.Door;
        }

        return FloodBarrier.None;
    }

    public static Color WaterlineColor(Color color)
    {
        return Color.InterpolateBetween(color, Color.White, WaterlineLighten)
            .WithAlpha(MathF.Min(color.A + WaterlineAlphaBoost, 1f));
    }

    /// <param name="depth">0 for a dry tile, 1 for a full one.</param>
    /// <param name="opacity">The liquid's own opacity at a full tile.</param>
    public void SetLevel(EntityUid puddle, float depth, Color color, float opacity)
    {
        var xform = Transform(puddle);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        _puddles[puddle] = (grid, indices);

        if (!Grids.TryGetValue(grid, out var tiles))
            Grids[grid] = tiles = new Dictionary<Vector2i, WetTile>();

        if (depth < MinDepth)
        {
            tiles.Remove(indices);
            return;
        }

        if (!tiles.TryGetValue(indices, out var tile))
            tiles[indices] = tile = new WetTile();

        tile.Depth = depth;
        tile.Color = color;
        tile.Opacity = opacity;
    }

    private void OnPuddleShutdown(Entity<PuddleComponent> entity, ref ComponentShutdown args)
    {
        if (_puddles.Remove(entity, out var key) && Grids.TryGetValue(key.Grid, out var tiles))
            tiles.Remove(key.Tile);
    }
}
