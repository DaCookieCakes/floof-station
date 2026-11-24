using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;

/// <summary>
///     Requires the target to be the user itself.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SelfTargetRequirement : InvertableInteractionRequirement
{
    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto, InteractionAction.VerbDependencies deps)
    {
        return (args.Target == args.User) ^ Inverted;
    }
}