using Robust.Shared.GameStates;

namespace Content.Shared._Floof.MapLayer.Transition;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PassiveSonarComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 30f;

    [DataField, AutoNetworkedField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan? FadeTime = TimeSpan.FromSeconds(1.2);
}
