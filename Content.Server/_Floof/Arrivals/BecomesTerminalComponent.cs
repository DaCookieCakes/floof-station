namespace Content.Server._Floof.Arrivals;

/// <summary>
/// Marks the grid that becomes the arrivals terminal when its map is loaded as a station's
/// <c>StationArrivals.ArrivalsMap</c>.
/// </summary>
[RegisterComponent]
public sealed partial class BecomesTerminalComponent : Component;
