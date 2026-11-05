using Content.Shared._Floof.Consent.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Consent;

/// <summary>
///     The consent toggles this character has enabled, copied from the profile on spawn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsentTogglesComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<ConsentTogglePrototype>> Toggles = new();
}
