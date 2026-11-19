namespace Content.Server._Floof.MapLayer.Station;

[RegisterComponent, Access(typeof(StationMapLayersSystem))]
public sealed partial class StationMapLayersComponent : Component
{
    [DataField(required: true)]
    public List<MapLayerEntry> Layers = new();

    [ViewVariables]
    public List<EntityUid> OwnedMaps = new();
}
