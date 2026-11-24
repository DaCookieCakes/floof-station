using Content.Shared._Floof.Sex.Components;
using Content.Shared._Floof.Sex.Events;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared._Floof.Sex;

public sealed partial class GenitalSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProducerGenitalComponent, BodyRelayedEvent<GenitaliaSolutionTransferEvent>>(OnTransferEvent);
    }

    private void OnTransferEvent(Entity<ProducerGenitalComponent> ent, ref BodyRelayedEvent<GenitaliaSolutionTransferEvent> @event)
    {
        if (!_solution.TryGetSolution(ent.Owner, @event.Args.SourceSolution, out _, out var solution, false))
            return;

        _solution.TryTransferSolution(@event.Args.TargetSolution, solution, @event.Args.Amount);
        
    }
}