using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;

/// <summary>
///     Requires the mob to be a mob in a certain state. If inverted, requires the mob to not be in that state.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MobStateRequirement : InvertableInteractionRequirement
{
    [DataField] public List<MobState> AllowedStates = new();

    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto, InteractionAction.VerbDependencies deps)
    {
        if (!deps.EntMan.TryGetComponent<MobStateComponent>(args.Target, out var state))
            return false;

        return AllowedStates.Contains(state.CurrentState) ^ Inverted;
    }
}

