using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.MapLayer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMapLayerSystem), Other = AccessPermissions.None)]
public sealed partial class MapLayerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Stack;

    [DataField, AutoNetworkedField]
    public bool NoOp;

    [DataField, AutoNetworkedField]
    public float Z;

    [DataField, AutoNetworkedField]
    public float BelowRange = 30f;

    [DataField, AutoNetworkedField]
    public float AboveFade = 8f;

    [DataField, AutoNetworkedField]
    public float RoofedAboveFade = 2f;

    [DataField, AutoNetworkedField]
    public float RoofFadeStart = 4f;

    [DataField, AutoNetworkedField]
    public float RoofFadeEnd = 6f;

    [DataField, AutoNetworkedField]
    public ResPath RoofTexture = new("/Textures/Tiles/hull.png");

    [DataField, AutoNetworkedField]
    public Color RoofColor = Color.FromHex("#707070");
}
