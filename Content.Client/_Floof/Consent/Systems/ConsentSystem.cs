using Content.Shared._Floof.Consent;
using Content.Shared._Floof.Consent.Prototypes;
using Content.Shared._Floof.Consent.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Consent.Systems;

public sealed class ConsentSystem : SharedConsentSystem
{
    public void RequestUpdate(string text, HashSet<ProtoId<ConsentTogglePrototype>> toggles)
    {
        RaiseNetworkEvent(new ConsentUpdateRequestEvent(text, toggles));
    }
}
