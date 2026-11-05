using Content.Shared.Body;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;

/// <summary>
///     Requires the target to have an organ.
///     I made this class before I looked at the intended interactions with organs so.
///     If you want to use this with genitals, don't, for other organs it's probably still a bad idea.
///     Why don't you want to use this? Because it assumes the structure of the body, and it doesn't own that.
///     wizden could change the structure of the body at any time and this class would no longer work, silently introduce bugs.
///     
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BodyOrganRequirement : InteractionRequirement
{
    [DataField] public required ProtoId<OrganCategoryPrototype> OrganCategory;

    [DataField] public bool TargetSelf = false;
    
    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto,
        InteractionAction.VerbDependencies deps)
    {
        var target = TargetSelf ? args.User : args.Target;
        var containerSystem = deps.EntMan.System<SharedContainerSystem>();
        if (!deps.EntMan.TryGetComponent<ContainerManagerComponent>(target, out var containerComp))
            return false;

        if (!containerSystem.TryGetContainer(target, BodyComponent.ContainerID, out var container, containerComp))
            return false;

        foreach (var organ in container.ContainedEntities)
        {
            if (!deps.EntMan.TryGetComponent<OrganComponent>(organ, out var organComp))
                return false;

            if (organComp.Category == OrganCategory)
                return true;
        }
        
        return false;
    }
}