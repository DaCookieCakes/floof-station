using Content.Shared._Floof.Consent.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Consent;

/// <summary>
///     Sent by the in-round consent window to change the consent of the player's current body and its character slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class ConsentUpdateRequestEvent(string text, HashSet<ProtoId<ConsentTogglePrototype>> toggles) : EntityEventArgs
{
    public readonly string Text = text;
    public readonly HashSet<ProtoId<ConsentTogglePrototype>> Toggles = toggles;
}
