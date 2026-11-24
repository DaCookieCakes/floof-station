using Robust.Shared.Serialization;

namespace Content.Shared._Floof.MapLayer.Transit;

[Serializable, NetSerializable]
public sealed class TransitLiftMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TransitLandMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TransitDockMessage : BoundUserInterfaceMessage;
