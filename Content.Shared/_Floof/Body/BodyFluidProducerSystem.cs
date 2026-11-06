using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Body;

public sealed partial class BodyFluidProducerSystem : EntitySystem
{
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyFluidProducerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, BodyFluidProducerComponent component, MapInitEvent args)
    {
        component.NextGrowth = _timing.CurTime + component.GrowthDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<BodyFluidProducerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextGrowth)
                continue;

            comp.NextGrowth += comp.GrowthDelay;

            if (!TryComp(uid, out OrganComponent? organ))
                continue;

            if (organ.Body is not EntityUid body) //I haven't tested this, probably should but surgery isn't a thing yet
                continue;

            if (_mobState.IsDead(body))
                continue;

            if (!_solutionContainerSystem.ResolveSolution(uid, comp.SolutionName, ref comp.Solution, out var solution))
                continue;

            if (solution.AvailableVolume == 0)
                continue;

            if (!TryComp(body, out SatiationComponent? satiation))
                continue;
            var entity = (body, satiation);
            
            if (_satiation.IsValueInRange(entity, SatiationSystem.Hunger, below: comp.MinHungerThreshold))
                continue;
            
            if (_satiation.IsValueInRange(entity, SatiationSystem.Thirst, below: comp.MinHungerThreshold))
                continue;

            _solutionContainerSystem.TryAddReagent(comp.Solution.Value, comp.ReagentId, comp.QuantityPerUpdate,
                out var quantity);
            
            if (quantity == 0)
                continue;
            _satiation.ModifyValue(entity,SatiationSystem.Hunger, -comp.HungerUsage * quantity.Float());
            _satiation.ModifyValue(entity,SatiationSystem.Thirst, -comp.HungerUsage * quantity.Float());
            
        }
    }
}