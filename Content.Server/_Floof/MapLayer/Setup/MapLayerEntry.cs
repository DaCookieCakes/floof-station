using Content.Server.Shuttles.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Station.Systems;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.MapLayer.Setup;

[ImplicitDataDefinitionForInheritors, UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract partial class MapLayerEntry
{
    [DataField(required: true)]
    public float Z;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public ComponentRegistry? Components;

    public virtual bool NoOp => false;

    public abstract EntityUid? GetMap(Entity<StationDataComponent> station, IEntityManager entMan);
}

public sealed partial class StationMapLayer : MapLayerEntry
{
    public override EntityUid? GetMap(Entity<StationDataComponent> station, IEntityManager entMan)
    {
        return entMan.System<StationSystem>().GetLargestGrid((station, station.Comp)) is { } grid
            ? entMan.GetComponent<TransformComponent>(grid).MapUid
            : null;
    }
}

public sealed partial class EmptyMapLayer : MapLayerEntry
{
    public override EntityUid? GetMap(Entity<StationDataComponent> station, IEntityManager entMan)
    {
        return entMan.System<SharedMapSystem>().CreateMap();
    }
}

public sealed partial class NoOpMapLayer : MapLayerEntry
{
    public override bool NoOp => true;

    public override EntityUid? GetMap(Entity<StationDataComponent> station, IEntityManager entMan)
    {
        return entMan.System<SharedMapSystem>().CreateMap();
    }
}

public sealed partial class ArrivalsMapLayer : MapLayerEntry
{
    public override EntityUid? GetMap(Entity<StationDataComponent> station, IEntityManager entMan)
    {
        return entMan.System<ServerArrivalsSystem>().EnsureTerminal(station);
    }
}
