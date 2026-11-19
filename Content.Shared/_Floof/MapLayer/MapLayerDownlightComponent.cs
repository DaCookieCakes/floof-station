namespace Content.Shared._Floof.MapLayer;

[RegisterComponent]
public sealed partial class MapLayerDownlightComponent : Component
{
    [DataField]
    public float Radius = 1f;

    [DataField]
    public float Spread = 0.6f;

    [DataField]
    public float Tilt;

    [DataField]
    public float Energy = 6f;

    [DataField]
    public float Reach = 20f;
}
