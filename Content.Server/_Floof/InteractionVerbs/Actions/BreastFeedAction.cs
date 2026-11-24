using Content.Shared.Bed.Sleep;
using Content.Shared.InteractionVerbs;
using Content.Shared.Mobs.Components;

namespace Content.Server.InteractionVerbs.Actions;

[Serializable]
public sealed partial class BreastFeedAction : InteractionAction
{
    [DataField]
    public bool WakeUp = false, Sleep = false;

    public override bool IsAllowed(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {

        return true;
    }

    public override bool CanPerform(InteractionArgs args, InteractionVerbPrototype proto, bool isBefore, VerbDependencies deps)
    {
        return true; // We already checked the rest in IsAllowed
    }

    public override bool Perform(InteractionArgs args, InteractionVerbPrototype proto, VerbDependencies deps)
    {

        return false;
    }
}
