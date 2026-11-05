// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Traits.Effects;

public sealed partial class RemLangEffect : BaseTraitEffect
{
    /// <summary>
    ///     The list of all Spoken Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>>? RemoveLanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<ProtoId<LanguagePrototype>>? RemoveLanguagesUnderstood { get; private set; } = default!;

    // Starlight - End
    public override void Apply(TraitEffectContext ctx)
    {
        var language = ctx.EntMan.System<SharedLanguageSystem>();

        if (RemoveLanguagesSpoken is not null)
            foreach (var lang in RemoveLanguagesSpoken)
                language.RemoveLanguage(ctx.Player, lang, true, false);

        if (RemoveLanguagesUnderstood is not null)
            foreach (var lang in RemoveLanguagesUnderstood)
                language.RemoveLanguage(ctx.Player, lang, false, true);
    }
}
