using Robust.Shared.Serialization;

namespace Content.Shared._Floof.MapLayer;

[DataDefinition, Serializable, NetSerializable]
public partial record struct MapLayerStack
{
    [DataField]
    public int Id;
}
