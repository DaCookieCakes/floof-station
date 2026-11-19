using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Ocean;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MapFluidComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "Seawater";

    [DataField, AutoNetworkedField]
    public float SurfacePressure = 101.325f;

    [DataField, AutoNetworkedField]
    public float DeepPressure = 600f;

    [DataField, AutoNetworkedField]
    public float FullPressureDepth = 240f;
}
