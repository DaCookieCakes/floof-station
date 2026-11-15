using Content.Shared.FixedPoint;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Raised on a puddle whenever its solution changed.
/// </summary>
[ByRefEvent]
public readonly record struct PuddleVolumeChangedEvent(FixedPoint2 Volume);
