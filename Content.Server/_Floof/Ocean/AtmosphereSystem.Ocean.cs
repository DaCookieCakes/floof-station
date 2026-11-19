using Content.Shared._Floof.Ocean;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private EntityQuery<MapFluidComponent> _mapFluidQuery = default!;

    private void InitializeOcean()
    {
        SubscribeLocalEvent<MapFluidComponent, ComponentStartup>(OnSeaChanged);
        SubscribeLocalEvent<MapFluidComponent, ComponentShutdown>(OnSeaChanged);
    }

    private void OnSeaChanged<T>(Entity<MapFluidComponent> ent, ref T args)
    {
        var grids = EntityQueryEnumerator<GridAtmosphereComponent, MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var atmos, out _, out var xform))
        {
            if (xform.MapUid != ent.Owner)
                continue;

            foreach (var tile in atmos.MapTiles)
            {
                InvalidateTile((uid, atmos), tile.GridIndices);
            }
        }
    }

    public void GetOceanBreaches(GridAtmosphereComponent atmos, List<Vector2i> breaches)
    {
        foreach (var outside in atmos.MapTiles)
        {
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection) (1 << i);
                if (outside.AirtightData.BlockedDirections.IsFlagSet(direction) ||
                    !atmos.Tiles.TryGetValue(outside.GridIndices.Offset(direction), out var inside) ||
                    inside.MapAtmosphere ||
                    inside.AirtightData.BlockedDirections.IsFlagSet(direction.GetOpposite()))
                    continue;

                breaches.Add(inside.GridIndices);
            }
        }
    }

    private void UpdateOceanSeal(GridAtmosphereComponent atmos, TileAtmosphere tile, bool exposed, EntityUid? map)
    {
        var wasExposed = tile.OceanExposed;
        tile.OceanExposed = exposed && _mapFluidQuery.HasComp(map);

        if (tile.OceanExposed != wasExposed && tile.ExcitedGroup != null)
            ExcitedGroupDispose(atmos, tile.ExcitedGroup);
    }
}
