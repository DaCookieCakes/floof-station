using Content.Server.Fluids.EntitySystems;
using Content.Shared._Floof.Sex.Components;
using Content.Shared.Body;
using Content.Shared.Buckle.Components;
using Content.Shared._Euphoria.Lewd.Mount.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.InteractionVerbs;

namespace Content.Server._Floof.InteractionVerbs.Actions.Lewd;

/// <summary>
///     Spills fluid from the user's organs as a puddle under the mount they are buckled onto.
/// </summary>
public sealed partial class LewdFillStructureFromTarget : InteractionAction
{
    [DataField] public required string SourceSolution;

    /// <summary>
    ///     How much fluid to pull from the organ per perform.
    /// </summary>
    [DataField] public FixedPoint2 Amount = 20;

    public override bool IsAllowed(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        return IsBuckledToTarget(args, deps);
    }

    /// <summary>
    ///     The verb is used on the mount itself, so the user must be buckled to that mount specifically.
    /// </summary>
    private static bool IsBuckledToTarget(InteractionArgs args, VerbDependencies deps)
    {
        return deps.EntMan.HasComponent<BreedingMountComponent>(args.Target)
            && deps.EntMan.TryGetComponent(args.User, out BuckleComponent? buckComp)
            && buckComp.BuckledTo == args.Target;
    }

    public override bool CanPerform(InteractionArgs args,
        InteractionVerbPrototype proto,
        bool beforeDelay,
        VerbDependencies deps)
    {
        return true;
    }

    public override bool Perform(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {
        var bodySystem = deps.EntMan.System<BodySystem>();
        var solutionContainer = deps.EntMan.System<SharedSolutionContainerSystem>();
        var puddleSystem = deps.EntMan.System<PuddleSystem>();

        if (!IsBuckledToTarget(args, deps))
            return false;

        var structure = args.Target;

        // Find the organ producing the requested fluid
        foreach (var organ in bodySystem.EnumerateOrgans<ProducerGenitalComponent>(args.User))
        {
            if (organ.Comp2.SolutionId != SourceSolution
                || !solutionContainer.TryGetSolution(organ.Owner, SourceSolution, out var source, out _, false))
                continue;

            // Pull the fluid out of the organ and dump it on the floor under the structure.
            var spilled = solutionContainer.SplitSolution(source.Value, Amount);
            if (spilled.Volume <= 0)
                return false;

            return puddleSystem.TrySpillAt(structure, spilled, out _);
        }

        return false;
    }
}
