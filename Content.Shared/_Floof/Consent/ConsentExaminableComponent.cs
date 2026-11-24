using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Consent;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsentExaminableComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string Content = string.Empty;
}
