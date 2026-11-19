using Content.Shared._Floof.MapLayer;
using Content.Shared.Shuttles.Components;

namespace Content.Server._Floof.MapLayer.Transition;

public sealed partial class LayerTransitSystem
{
    [SubscribeLocalEvent]
    private void OnSeabedMapInit(Entity<ShuttleComponent> ent, ref MapInitEvent args)
    {
        AnchorOnSeabed(ent);
    }

    [SubscribeLocalEvent]
    private void OnSeabedParentChanged(Entity<ShuttleComponent> ent, ref EntParentChangedMessage args)
    {
        AnchorOnSeabed(ent);
    }

    private void AnchorOnSeabed(EntityUid grid)
    {
        if (Transform(grid).MapUid is { } map && HasComp<MapLayerSeabedComponent>(map))
            _shuttle.Disable(grid);
    }
}
