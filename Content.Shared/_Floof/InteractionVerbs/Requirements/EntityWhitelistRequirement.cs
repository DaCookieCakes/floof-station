using Content.Shared.Whitelist;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs.Requirements;

/// <summary>
///     Requires the target to meet a certain whitelist and not meet a blacklist.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class EntityWhitelistRequirement : InteractionRequirement
{
    [DataField] public EntityWhitelist? Whitelist, Blacklist;

    public override bool IsMet(InteractionArgs args, InteractionVerbPrototype proto, InteractionAction.VerbDependencies deps) =>
        !deps.WhitelistSystem.IsWhitelistFail(Whitelist, args.Target)
        && !deps.WhitelistSystem.IsWhitelistPass(Blacklist, args.Target);
}