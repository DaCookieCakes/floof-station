using Content.Shared._Floof.Sex.Events;
using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.InteractionVerbs;

namespace Content.Server.InteractionVerbs.Actions;

public sealed partial class OrganSolutionTransferAction : InteractionAction
{
    [DataField] public bool TargetSelf = false;
    [DataField] public required string SourceSolution;
    
    public override bool CanPerform(InteractionArgs args, InteractionVerbPrototype proto, bool beforeDelay, VerbDependencies deps)
    {
        return true;
    }

    public override bool Perform(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        var bodySystem = deps.EntMan.System<BodySystem>();
        var solutionContainer = deps.EntMan.System<SharedSolutionContainerSystem>();


        if (!solutionContainer.TryGetRefillableSolution(args.Target, out var entity, out var solution))
            return false;
        
        if (!deps.EntMan.TryGetComponent<BodyComponent>(args.User, out var bodyComponent))
            return false;

        var transferEvent = new GenitaliaSolutionTransferEvent
        {
            TargetSolution = entity.Value,
            SourceSolution = SourceSolution
        };

        Entity<BodyComponent> bodyEntity = (args.User, bodyComponent);
        bodySystem.RelayEvent(bodyEntity, ref transferEvent);
        
        return true;
    }
}