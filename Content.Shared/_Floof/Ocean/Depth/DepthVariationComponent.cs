using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Ocean.Depth;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDepthSystem))]
public sealed partial class DepthVariationComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public int Seed;
}
