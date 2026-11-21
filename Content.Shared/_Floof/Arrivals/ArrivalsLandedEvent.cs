using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Arrivals;

[Serializable, NetSerializable]
public sealed class ArrivalsLandedEvent : EntityEventArgs;
