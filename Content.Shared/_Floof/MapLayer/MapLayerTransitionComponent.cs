using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.MapLayer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMapLayerSystem), Other = AccessPermissions.None)]
public sealed partial class MapLayerTransitionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StartZ;

    [DataField, AutoNetworkedField]
    public float EndZ;

    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration;

    [DataField, AutoNetworkedField]
    public MapLayerEasing Easing = MapLayerEasing.InOutSine;
}

[Serializable, NetSerializable]
public enum MapLayerEasing : byte
{
    Linear,
    InOutSine,
    InOutCubic,
    InSine,
    OutSine,
}
