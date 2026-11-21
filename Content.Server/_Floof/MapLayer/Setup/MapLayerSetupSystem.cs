using Content.Server.Station.Events;
using Content.Shared._Floof.MapLayer;

namespace Content.Server._Floof.MapLayer.Setup;

public sealed partial class MapLayerSetupSystem : EntitySystem
{
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    [SubscribeLocalEvent]
    private void OnStationPostInit(Entity<MapLayerSetupComponent> ent, ref StationPostInitEvent args)
    {
        var stack = _layers.CreateStack();
        foreach (var entry in ent.Comp.Layers)
        {
            if (entry.GetMap(args.Station, EntityManager) is not { } layer)
                continue;

            if (HasComp<MapLayerComponent>(layer))
            {
                Log.Error($"{ToPrettyString(layer)} is already a map layer, not adding it as {entry.Name}");
                continue;
            }

            _layers.SetLayer(layer, stack, entry.Z, entry.NoOp);

            if (entry.Components != null)
                EntityManager.AddComponents(layer, entry.Components);

            _metaData.SetEntityName(layer, $"{entry.Name} (Z {entry.Z})");
        }
    }
}
