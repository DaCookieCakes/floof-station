using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Ocean;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MapFluidComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "Seawater";
}
