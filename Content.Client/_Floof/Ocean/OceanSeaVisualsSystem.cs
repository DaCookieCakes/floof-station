using Content.Client.Fluids;
using Content.Shared._Floof.Ocean;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Ocean;

/// <summary>
///     How the sea of an <see cref="OceanSeaComponent"/> map looks where it stands on a grid.
/// </summary>
public sealed partial class OceanSeaVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;

    [Dependency] private EntityQuery<OceanDepthComponent> _depthQuery = default!;
    [Dependency] private EntityQuery<OceanSeaComponent> _seaQuery = default!;

    /// <summary>
    ///     The sea's color as a full tile, and how high it stands on what's outside. That only climbs to full as a dive
    ///     goes under, along with the deep water drawn past the grid.
    /// </summary>
    public bool TryGetSea(MapId mapId, out Color color, out float level)
    {
        color = default;
        level = 0f;
        if (!_map.TryGetMap(mapId, out var map) ||
            !_seaQuery.TryComp(map, out var sea) ||
            !_proto.TryIndex<ReagentPrototype>(sea.Reagent, out var reagent))
            return false;

        level = _depthQuery.TryComp(map, out var depth) ? depth.GetSubmerged(_timing.CurTime) : 1f;
        color = _puddle.GetFlatColor(reagent.SubstanceColor, reagent.LiquidOpacity, 1f);
        return level > 0f;
    }
}
