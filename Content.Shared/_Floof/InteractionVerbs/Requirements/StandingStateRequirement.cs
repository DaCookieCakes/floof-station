using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;



/// <summary>
///     Requires the target to be in a specific standing state.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class StandingStateRequirement : InteractionRequirement
{
    [DataField] public bool AllowStanding, AllowLaying, AllowKnockedDown;

    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto, InteractionAction.VerbDependencies deps)
    {
        if (deps.EntMan.HasComponent<KnockedDownComponent>(args.Target))
            return AllowKnockedDown;

        if (!deps.EntMan.TryGetComponent<StandingStateComponent>(args.Target, out var state))
            return false;

        return state.Standing && AllowStanding
               || !state.Standing && AllowLaying;
    }
}