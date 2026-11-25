using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Carrying;

[Serializable, NetSerializable]
public sealed partial class CarryEscapeDoAfterEvent : SimpleDoAfterEvent;
