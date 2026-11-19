using Robust.Shared.GameStates;

namespace Content.Shared._Floof.MapLayer;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedMapLayerSystem))]
public sealed partial class MapLayerStackComponent : Component;
