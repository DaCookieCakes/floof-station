using Content.Shared.Chemistry.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Sex.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ConsumerGenitalComponent : Component
{
    [DataField]
    public string SolutionId = SolutionComponent.DefaultSolutionId;
    
    
    
}