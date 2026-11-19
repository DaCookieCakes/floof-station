using Robust.Shared.Serialization;

namespace Content.Shared._Floof.MapLayer.Transition;

[Serializable, NetSerializable]
public sealed class SubLiftMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SubLandMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class SubDockMessage : BoundUserInterfaceMessage;
