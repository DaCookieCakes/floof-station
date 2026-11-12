using Content.Shared._Floof.Consent.Prototypes;
using Content.Shared._Floof.Consent.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;

/// <summary>
///     Requires the target (or the user, with <see cref="TargetSelf"/>) to have a consent toggle enabled.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ConsentRequirement : InteractionRequirement
{
    [DataField(required: true)] public ProtoId<ConsentTogglePrototype> Toggle;

    [DataField] public bool TargetSelf = false;

    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto,
        InteractionAction.VerbDependencies deps)
    {
        var target = TargetSelf ? args.User : args.Target;
        return deps.EntMan.System<SharedConsentSystem>().HasConsent(target, Toggle);
    }
}
