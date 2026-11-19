using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.MapLayer.Transition;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SonarComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public float Range = 150f;

    [DataField, AutoNetworkedField]
    public TimeSpan Interval = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public TimeSpan? FadeTime;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#9fe8c8");
}

[Serializable, NetSerializable]
public sealed class SonarToggleMessage : BoundUserInterfaceMessage;
