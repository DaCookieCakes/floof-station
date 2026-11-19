using Content.Server.Station.Events;
using Content.Shared._Floof.MapLayer;

namespace Content.Server._Floof.MapLayer.Station;

public sealed partial class StationMapLayersSystem : EntitySystem
{
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    [SubscribeLocalEvent]
    private void OnStationPostInit(Entity<StationMapLayersComponent> ent, ref StationPostInitEvent args)
    {
        MapLayerEntry? rootEntry = null;
        var roots = 0;
        foreach (var entry in ent.Comp.Layers)
        {
            if (entry is not StationMapLayer)
                continue;

            rootEntry = entry;
            roots++;
        }

        if (roots != 1 || rootEntry == null)
        {
            Log.Error($"{ToPrettyString(ent)} needs exactly one StationMapLayer, has {roots}");
            return;
        }

        if (rootEntry.GetMap(args.Station, EntityManager) is not { } root)
        {
            Log.Error($"{ToPrettyString(ent)} has no station map to build its map layers on");
            return;
        }

        _layers.CreateStack(root, rootEntry.Z);
        foreach (var entry in ent.Comp.Layers)
        {
            var map = entry == rootEntry ? root : entry.GetMap(args.Station, EntityManager);
            if (map is not { } layer)
                continue;

            if (entry.Owned)
                ent.Comp.OwnedMaps.Add(layer);

            if (layer != root)
            {
                if (HasComp<MapLayerComponent>(layer))
                {
                    Log.Error($"{ToPrettyString(layer)} is already a map layer, not adding it as {entry.Name}");
                    continue;
                }

                _layers.SetLayer(layer, root, entry.Z, entry.NoOp);
            }

            if (entry.Components != null)
                EntityManager.AddComponents(layer, entry.Components);

            _metaData.SetEntityName(layer, $"{entry.Name} (Z {entry.Z})");
        }
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<StationMapLayersComponent> ent, ref ComponentShutdown args)
    {
        foreach (var map in ent.Comp.OwnedMaps)
        {
            QueueDel(map);
        }
    }
}
