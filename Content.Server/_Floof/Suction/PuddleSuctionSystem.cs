using Content.Shared._Floof.Suction;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Suction;

public sealed partial class PuddleSuctionSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    private readonly HashSet<Entity<PuddleComponent>> _puddles = [];

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PuddleSuctionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var suction, out var xform))
        {
            if (now < suction.NextUpdate)
                continue;

            suction.NextUpdate = now + suction.Interval;

            if (!_solution.TryGetSolution(uid, suction.Solution, out var tankEnt, out var tank)
                || tank.AvailableVolume <= 0)
                continue;

            var amount = suction.UnitsPerTile * suction.Interval.TotalSeconds;

            _puddles.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, suction.Range, _puddles);

            foreach (var puddle in _puddles)
            {
                if (tank.AvailableVolume <= 0)
                    break;

                if (!_solution.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref puddle.Comp.Solution, out var puddleSolution))
                    continue;

                var taken = _solution.SplitSolution(puddle.Comp.Solution.Value,
                    FixedPoint2.Min(amount, puddleSolution.Volume, tank.AvailableVolume));
                _solution.TryAddSolution(tankEnt.Value, taken);

                if (puddleSolution.Volume <= 0)
                    QueueDel(puddle);
            }
        }
    }
}
