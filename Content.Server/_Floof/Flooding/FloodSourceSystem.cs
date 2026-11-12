using Content.Server.Fluids.EntitySystems;
using Content.Shared._Floof.Flooding;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Runs sources and drains a few times a second, so they don't pulse.
/// </summary>
public sealed partial class FloodSourceSystem : EntitySystem
{
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;

    private const float Interval = 0.25f;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < Interval)
            return;

        _accumulator -= Interval;

        var sources = EntityQueryEnumerator<FloodSourceComponent, TransformComponent>();
        while (sources.MoveNext(out var source, out var xform))
        {
            if (TryGetTile(xform, out var tile))
                _puddle.TrySpillAt(tile, new Solution(source.Reagent, source.UnitsPerSecond * Interval), out _, false, false);
        }

        var drains = EntityQueryEnumerator<FloodDrainComponent, TransformComponent>();
        while (drains.MoveNext(out _, out var xform))
        {
            if (TryGetTile(xform, out var tile))
                Drain(tile);
        }
    }

    private void Drain(TileRef tile)
    {
        if (!_puddle.TryGetPuddle(tile, out var puddle) ||
            !_puddleQuery.TryComp(puddle, out var comp) ||
            !_solution.TryGetSolution(puddle, comp.SolutionName, out var solution))
            return;

        _solution.RemoveAllSolution(solution.Value);

        // Settled neighbours don't notice the hole on their own.
        var grid = _gridQuery.GetComponent(tile.GridUid);
        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var anchored = _map.GetAnchoredEntities(tile.GridUid, grid, tile.GridIndices.Offset((AtmosDirection) (1 << i)));
            while (anchored.MoveNext(out var ent))
            {
                if (_puddleQuery.HasComp(ent))
                    _puddle.WakePuddle(ent.Value);
            }
        }
    }

    private bool TryGetTile(TransformComponent xform, out TileRef tile)
    {
        tile = default;
        return xform.Anchored && _gridQuery.TryComp(xform.GridUid, out var grid) &&
               _map.TryGetTileRef(xform.GridUid.Value, grid, xform.Coordinates, out tile);
    }
}
