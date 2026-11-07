using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
///     Water stops gas like a wall would: a tile full of it holds on to its air until it drains.
/// </summary>
public sealed partial class AtmosphereSystem
{
    /// <summary>
    ///     Marks a tile as full of liquid, or not anymore.
    /// </summary>
    public void SetFloodFull(EntityUid grid, Vector2i indices, bool full)
    {
        if (!_gridAtmosQuery.TryComp(grid, out var atmos) || !atmos.Tiles.TryGetValue(indices, out var tile))
            return;

        tile.FloodFull = full;
        InvalidateTile((grid, atmos), indices);
    }

    private void UpdateFloodSeal(GridAtmosphereComponent atmos, TileAtmosphere tile)
    {
        // A group spanning the new seal would keep sharing across it.
        if (tile.FloodFull && tile.ExcitedGroup != null)
            ExcitedGroupDispose(atmos, tile.ExcitedGroup);
    }

    /// <summary>
    ///     The directions gas can't leave this tile in.
    /// </summary>
    private static AtmosDirection GetFloodBlocked(TileAtmosphere tile)
    {
        return tile.FloodFull ? AtmosDirection.All : tile.AirtightData.BlockedDirections;
    }
}
