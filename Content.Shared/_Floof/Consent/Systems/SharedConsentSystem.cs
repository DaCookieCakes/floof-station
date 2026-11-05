using Content.Shared._Floof.Consent.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Consent.Systems;

public abstract partial class SharedConsentSystem : EntitySystem
{
    public bool HasConsent(EntityUid uid, ProtoId<ConsentTogglePrototype> toggle)
    {
        return TryComp<ConsentTogglesComponent>(uid, out var comp) && comp.Toggles.Contains(toggle);
    }
}
