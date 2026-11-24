using Content.Client.Fluids;
using Content.Shared._Floof.Ocean;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Ocean;

public sealed partial class MapFluidVisualsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;

    [Dependency] private EntityQuery<MapFluidComponent> _fluidQuery = default!;

    public bool TryGetFluidColor(MapId mapId, out Color color)
    {
        color = default;
        if (!_map.TryGetMap(mapId, out var map) ||
            !_fluidQuery.TryComp(map, out var fluid) ||
            !_proto.TryIndex<ReagentPrototype>(fluid.Reagent, out var reagent))
            return false;

        color = _puddle.GetFlatColor(reagent.SubstanceColor, reagent.LiquidOpacity, 1f);
        return true;
    }
}
