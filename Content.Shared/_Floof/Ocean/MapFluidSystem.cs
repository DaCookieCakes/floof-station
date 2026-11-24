using System.Numerics;
using Content.Shared._Floof.Ocean.Depth;
using Content.Shared.Atmos;
using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;

namespace Content.Shared._Floof.Ocean;

public sealed partial class MapFluidSystem : EntitySystem
{
    [Dependency] private SharedDepthSystem _depth = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    [Dependency] private EntityQuery<BuckleComponent> _buckleQuery;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;
    [Dependency] private EntityQuery<MapFluidComponent> _fluidQuery;

    private const float SeawaterDensity = 1025f;
    private const float StandardGravity = 9.80665f;

    public bool HasFluid(EntityUid? map)
    {
        return _fluidQuery.HasComp(map);
    }

    public bool TryGetMapFluid(EntityUid? map, out Entity<MapFluidComponent> fluid)
    {
        fluid = default;
        if (map is not { } uid || !_fluidQuery.TryComp(uid, out var comp))
            return false;

        fluid = (uid, comp);
        return true;
    }

    public void CopyMapSettings(EntityUid map, EntityUid from)
    {
        if (_fluidQuery.TryComp(from, out var fluid))
            CopyComp(from, map, fluid);
        else
            RemComp<MapFluidComponent>(map);

        EnsureComp<MapLightComponent>(map);
        var gravity = EnsureComp<GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(map, gravity);
    }
    
    public bool IsInFluid(TransformComponent xform)
    {
        return TryGetFluid(xform, out _);
    }


    public bool TryGetFluid(TransformComponent xform, out Entity<MapFluidComponent> fluid)
    {
        if (!TryGetMapFluid(xform.MapUid, out fluid))
            return false;

        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return xform.ParentUid == xform.MapUid;

        return IsLoose(xform, gridUid) &&
               (gridUid == xform.MapUid || IsExposed(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates)));
    }
    
    public bool IsExposed(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        return !_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef);
    }

    public float GetPressure(Entity<MapFluidComponent> fluid, Vector2 position)
    {
        var depth = MathF.Max(_depth.GetDepth(fluid, position) ?? 0f, 0f);
        return Atmospherics.OneAtmosphere + SeawaterDensity * StandardGravity * depth / 1000f;
    }
    
    private bool IsLoose(TransformComponent xform, EntityUid gridUid)
    {
        return xform.ParentUid == gridUid ||
               _buckleQuery.TryComp(xform.Owner, out var buckle) && buckle.BuckledTo == xform.ParentUid &&
               Transform(xform.ParentUid).ParentUid == gridUid;
    }
}
