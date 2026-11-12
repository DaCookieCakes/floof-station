using Content.Server.Atmos.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Ocean;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Floof.Ocean;

/// <summary>
///     The sea pours in through every breach a few times a second, until the tile is full.
/// </summary>
public sealed partial class OceanInflowSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    [Dependency] private EntityQuery<OceanSeaComponent> _seaQuery = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;

    private const float Interval = 0.25f;

    private float _accumulator;
    private readonly List<Vector2i> _breaches = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < Interval)
            return;

        _accumulator -= Interval;

        var grids = EntityQueryEnumerator<GridAtmosphereComponent, MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var atmos, out var grid, out var xform))
        {
            if (_seaQuery.TryComp(xform.MapUid, out var sea))
                Inflow((uid, grid), atmos, sea);
        }
    }

    private void Inflow(Entity<MapGridComponent> grid, GridAtmosphereComponent atmos, OceanSeaComponent sea)
    {
        _breaches.Clear();
        _atmos.GetOceanBreaches(atmos, _breaches);

        foreach (var indices in _breaches)
        {
            var tile = _map.GetTileRef(grid, indices);
            if (IsFull(tile))
                continue;

            _puddle.TrySpillAt(tile, new Solution(sea.Reagent, sea.InflowPerSecond * Interval), out _, false, false);
        }
    }

    private bool IsFull(TileRef tile)
    {
        return _puddle.TryGetPuddle(tile, out var puddle) &&
               _puddleQuery.TryComp(puddle, out var comp) &&
               _solution.TryGetSolution(puddle, comp.SolutionName, out _, out var solution) &&
               solution.Volume >= FloodConstants.TileVolume;
    }
}
