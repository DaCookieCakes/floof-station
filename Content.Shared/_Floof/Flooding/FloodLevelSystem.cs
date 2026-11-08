using System.Numerics;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Ocean;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Reads how much liquid is where, from the puddle appearance data clients already get for drawing.
///     Both sides use it, so anything built on it predicts without state of its own.
/// </summary>
public sealed partial class FloodLevelSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private OceanSeaSystem _sea = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    [Dependency] private EntityQuery<BuckleComponent> _buckleQuery = default!;
    [Dependency] private EntityQuery<ClimbingComponent> _climbingQuery = default!;
    [Dependency] private EntityQuery<DoorComponent> _doorQuery = default!;
    [Dependency] private EntityQuery<FloodMaskComponent> _maskQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;

    private static readonly Vector2i[] Neighbors = { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right };

    private float _currentDeadzone;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentDeadzone, value => _currentDeadzone = value, true);
    }

    public static float VolumeToDepth(float volume)
    {
        return MathF.Sqrt(Math.Clamp(volume / FloodConstants.TileVolume.Float(), 0f, 1f));
    }

    public float GetDepth(TransformComponent xform)
    {
        return VolumeToDepth(GetVolume(xform));
    }

    public float GetVolume(EntityUid puddle, PuddleComponent comp)
    {
        if (!_appearance.TryGetData<float>(puddle, PuddleVisuals.CurrentVolume, out var volume))
            return 0f;

        var overflow = comp.OverflowVolume;
        return volume * overflow.Float();
    }

    public float GetVolume(TransformComponent xform)
    {
        if (_sea.IsInSea(xform))
            return FloodConstants.TileVolume.Float();

        if (xform.GridUid is not { } gridUid || !IsLoose(xform, gridUid) || !_gridQuery.TryComp(gridUid, out var grid))
            return 0f;

        return GetVolume(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
    }

    public float GetVolume(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_puddleQuery.TryComp(ent, out var puddle))
                return GetVolume(ent.Value, puddle);
        }

        return 0f;
    }

    /// <summary>
    ///     The puddle the entity is standing in.
    /// </summary>
    public bool TryGetPuddle(TransformComponent xform, out Entity<PuddleComponent> puddle)
    {
        puddle = default;
        if (xform.GridUid is not { } gridUid || !IsLoose(xform, gridUid) || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
        while (anchored.MoveNext(out var ent))
        {
            if (!_puddleQuery.TryComp(ent, out var comp))
                continue;

            puddle = (ent.Value, comp);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Out in the open: not while held, in a locker and so on. Buckling parents you to the chair, which doesn't keep you dry.
    /// </summary>
    public bool IsLoose(TransformComponent xform, EntityUid gridUid)
    {
        return xform.ParentUid == gridUid ||
               _buckleQuery.TryComp(xform.Owner, out var buckle) && buckle.BuckledTo == xform.ParentUid &&
               Transform(xform.ParentUid).ParentUid == gridUid;
    }

    /// <summary>
    ///     The height of the highest surface on this tile.
    /// </summary>
    public float GetSurface(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var surface = 0f;
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_maskQuery.TryComp(ent, out var mask))
                surface = MathF.Max(surface, mask.Surface);
        }

        return surface;
    }

    /// <summary>
    ///     Whether this sits on top of whatever surface shares its tile: loose objects do, mobs only once they've climbed up.
    /// </summary>
    public bool RestsOnSurface(EntityUid uid, TransformComponent xform)
    {
        if (_climbingQuery.TryComp(uid, out var climbing))
            return climbing.IsClimbing;

        return !xform.Anchored && !(_maskQuery.TryComp(uid, out var mask) && mask.Surface > 0f);
    }

    /// <summary>
    ///     Water runs downhill: every neighbour pulls towards itself or pushes away by how much the two levels differ.
    ///     Walls and closed doors don't take part, a dry floor or the void beyond the hull is simply empty.
    ///     Under the sea that void is a full tile instead, so settled water along the hull doesn't run out into it.
    /// </summary>
    public Vector2 GetCurrent(EntityUid gridUid, MapGridComponent grid, Vector2i tile, float volume)
    {
        var current = Vector2.Zero;
        var underSea = _sea.IsUnderSea(Transform(gridUid).MapUid);
        foreach (var offset in Neighbors)
        {
            var neighbor = tile + offset;
            var other = underSea && _sea.IsExposed(gridUid, grid, neighbor)
                ? FloodConstants.TileVolume.Float()
                : GetVolume(gridUid, grid, neighbor);
            var deeper = MathF.Max(volume, other);
            var difference = (volume - other) / deeper;
            if (MathF.Abs(difference) < _currentDeadzone || IsWaterBlocked(gridUid, grid, neighbor))
                continue;

            current += (Vector2) offset * difference * 0.5f;
        }

        return current;
    }

    public bool IsWaterBlocked(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // Airlock layers don't include Impassable, so a shut door would read as an empty tile.
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (_doorQuery.TryComp(ent, out var door) && door.State is not (DoorState.Open or DoorState.Opening))
                return true;
        }

        return _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid);
    }

    /// <summary>
    ///     The colour of the liquid the entity is in, puddle or sea.
    /// </summary>
    public bool TryGetLiquidColor(TransformComponent xform, out Color color)
    {
        color = default;
        if (_sea.TryGetSea(xform, out var sea))
        {
            if (!_proto.TryIndex(sea.Comp.Reagent, out var reagent))
                return false;

            color = reagent.SubstanceColor;
            return true;
        }

        if (!TryGetPuddle(xform, out var puddle))
            return false;

        color = GetColor(puddle);
        return true;
    }

    /// <summary>
    ///     Pressure in kPa the sea adds where this entity stands: out in it, or in a flooded tile that is mostly its
    ///     water. A spilled tank doesn't crush anyone.
    /// </summary>
    public float GetSeaPressure(EntityUid uid)
    {
        var xform = Transform(uid);
        if (!_sea.TryGetMapSea(xform.MapUid, out var sea))
            return 0f;

        if (_sea.IsInSea(xform))
            return _sea.GetPressure(sea);

        if (GetDepth(xform) < sea.Comp.PressureDepth ||
            !TryGetPuddle(xform, out var puddle) ||
            !_solution.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref puddle.Comp.Solution, out var solution) ||
            solution.Volume <= 0)
            return 0f;

        var share = (solution.GetTotalPrototypeQuantity(sea.Comp.Reagent) / solution.Volume).Float();
        return share > sea.Comp.MinShare ? _sea.GetPressure(sea) : 0f;
    }

    public Color GetColor(EntityUid puddle)
    {
        return _appearance.TryGetData<Color>(puddle, PuddleVisuals.SolutionColor, out var color) ? color : Color.White;
    }

    /// <summary>
    ///     Ghosts, revenants, jaunts and the AI eye pass through liquid without touching it.
    /// </summary>
    public bool IsIntangible(EntityUid uid)
    {
        return HasComp<SpectralComponent>(uid) || HasComp<MovementIgnoreGravityComponent>(uid);
    }
}
