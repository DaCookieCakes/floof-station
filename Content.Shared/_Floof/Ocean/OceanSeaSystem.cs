using Content.Shared.Buckle.Components;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Ocean;

/// <summary>
///     Where the sea of an <see cref="OceanSeaComponent"/> map is: every tile open to the map, and past the grids.
/// </summary>
public sealed partial class OceanSeaSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    [Dependency] private EntityQuery<BuckleComponent> _buckleQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<OceanDepthComponent> _depthQuery = default!;
    [Dependency] private EntityQuery<OceanSeaComponent> _seaQuery = default!;

    public bool IsUnderSea(EntityUid? map)
    {
        return _seaQuery.HasComp(map);
    }

    public bool TryGetMapSea(EntityUid? map, out Entity<OceanSeaComponent> sea)
    {
        sea = default;
        if (map is not { } uid || !_seaQuery.TryComp(uid, out var comp))
            return false;

        sea = (uid, comp);
        return true;
    }

    /// <summary>
    ///     Whether the entity is out in the sea itself, loose on an exposed tile or past the edge of the grid.
    /// </summary>
    public bool IsInSea(TransformComponent xform)
    {
        return TryGetSea(xform, out _);
    }

    /// <inheritdoc cref="IsInSea"/>
    public bool TryGetSea(TransformComponent xform, out Entity<OceanSeaComponent> sea)
    {
        if (!TryGetMapSea(xform.MapUid, out sea))
            return false;

        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return xform.ParentUid == xform.MapUid;

        return IsLoose(xform, gridUid) &&
               (gridUid == xform.MapUid || IsExposed(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates)));
    }

    /// <summary>
    ///     Open to the map: no tile, an empty one, or space like lattice.
    /// </summary>
    public bool IsExposed(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        return !_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef);
    }

    /// <summary>
    ///     How hard the sea presses in kPa right now, climbing with the map's depth.
    /// </summary>
    public float GetPressure(Entity<OceanSeaComponent> sea)
    {
        if (!_depthQuery.TryComp(sea, out var depth))
            return sea.Comp.DeepPressure;

        var share = MathF.Min(depth.GetDepth(_timing.CurTime) / MathF.Max(sea.Comp.FullPressureDepth, 0.001f), 1f);
        return sea.Comp.SurfacePressure + (sea.Comp.DeepPressure - sea.Comp.SurfacePressure) * share;
    }

    /// <summary>
    ///     Out in the open: not while held, in a locker and so on. Buckling parents you to the chair, which doesn't keep you dry.
    /// </summary>
    private bool IsLoose(TransformComponent xform, EntityUid gridUid)
    {
        return xform.ParentUid == gridUid ||
               _buckleQuery.TryComp(xform.Owner, out var buckle) && buckle.BuckledTo == xform.ParentUid &&
               Transform(xform.ParentUid).ParentUid == gridUid;
    }
}
