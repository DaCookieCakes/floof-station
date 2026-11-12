using Content.Shared.Atmos;
using Content.Shared.FixedPoint;

namespace Content.Shared._Floof.Flooding;

public static class FloodConstants
{
    public const int UnitsPerLiter = 120;

    /// <summary>
    ///     Floor to ceiling of one tile, same volume atmos uses for a tile. Puddles cap here.
    /// </summary>
    public static readonly FixedPoint2 TileVolume = Atmospherics.CellVolume * UnitsPerLiter;
}
