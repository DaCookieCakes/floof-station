using Content.Shared.Atmos;
using Content.Shared.FixedPoint;

namespace Content.Shared._Floof.Flooding;

public static class FloodConstants
{
    public const int UnitsPerLiter = 120;

    public static readonly FixedPoint2 TileVolume = Atmospherics.CellVolume * UnitsPerLiter;

    public const float MinShare = 0.5f;

    public const float PressureDepth = 0.9f;
}
