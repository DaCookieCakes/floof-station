namespace Content.Server._Floof.FathomGate;

/// <summary>
/// Raised on both gates of a pair when it opens.
/// </summary>
[ByRefEvent]
public record struct FathomGateOpenedEvent;

/// <summary>
/// Raised on both gates of a pair when the link collapses.
/// </summary>
[ByRefEvent]
public record struct FathomGateCollapsedEvent;
