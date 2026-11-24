using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Ocean.Depth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDepthSystem))]
public sealed partial class DepthTransitionComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float StartDepth;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public float EndDepth;
}
