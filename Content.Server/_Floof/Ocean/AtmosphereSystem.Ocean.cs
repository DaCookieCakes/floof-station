using Content.Shared._Floof.Ocean;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
///     Under the sea, what's open to the map is water, so nothing vents into it.
/// </summary>
public sealed partial class AtmosphereSystem
{
    [Dependency] private EntityQuery<OceanSeaComponent> _oceanSeaQuery = default!;

    private void InitializeOcean()
    {
        SubscribeLocalEvent<OceanSeaComponent, ComponentStartup>(OnSeaChanged);
        SubscribeLocalEvent<OceanSeaComponent, ComponentShutdown>(OnSeaChanged);
    }

    private void OnSeaChanged<T>(Entity<OceanSeaComponent> ent, ref T args)
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

    /// <summary>
    ///     Adds every tile bordering one open to the map, with no wall, closed door and so on in between. Once per side
    ///     it's open on, so a tile with more openings fills faster.
    /// </summary>
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
        tile.OceanExposed = exposed && _oceanSeaQuery.HasComp(map);

        // A group spanning the new seal would keep sharing across it.
        if (tile.OceanExposed != wasExposed && tile.ExcitedGroup != null)
            ExcitedGroupDispose(atmos, tile.ExcitedGroup);
    }
}
