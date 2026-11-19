namespace Content.Server._Floof.MapLayer;

[RegisterComponent, Access(typeof(MapLayerSystem))]
public sealed partial class MapLayerPoolComponent : Component
{
    [ViewVariables]
    public bool InUse;
}
