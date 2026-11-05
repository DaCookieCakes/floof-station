using Content.Shared._Starlight.Language.Systems;
using Content.Shared.Abilities.Mime;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Puppet;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.Speech.Muting;

/// <summary>
/// Handles the speech restrictions imposed by <see cref="MutedStatusEffectComponent"/>.
/// </summary>
public sealed partial class MutedStatusEffectSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedLanguageSystem _languages = default!; // Starlight

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<SpeakAttemptEvent>>(OnSpeakAttempt);
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<EmoteEvent>>(OnEmote);
        SubscribeLocalEvent<MutedStatusEffectComponent, StatusEffectRelayedEvent<EmoteActionEvent>>(OnEmoteAction);
    }

    private void OnEmote(Entity<MutedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<EmoteEvent> args)
    {
        if (args.Args.Handled)
            return;

        // Still leaves the text so it looks like they are pantomiming a laugh.
        if (args.Args.Emote.Category.HasFlag(EmoteCategory.Vocal))
        {
            args.Args = args.Args with { Handled = true };
        }
    }

    private void OnEmoteAction(Entity<MutedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<EmoteActionEvent> args)
    {
        if (args.Args.Handled)
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.ActionPopup), args.AppliedTo, args.AppliedTo);
        args.Args.Handled = true;
    }

    private void OnSpeakAttempt(Entity<MutedStatusEffectComponent> ent, ref StatusEffectRelayedEvent<SpeakAttemptEvent> args)
    {
        // TODO something better than this.

        // Starlight-start: Cannot mute if there's no speech involved
        var language = _languages.GetLanguage(ent.Owner);
        if (!language.SpeechOverride.RequireSpeech)
            return;
        // Starlight-end

        if (HasComp<MimePowersComponent>(ent.Owner))
            _popup.PopupEntity(Loc.GetString("mime-cant-speak"), ent.Owner, ent.Owner);
        else if (HasComp<VentriloquistPuppetComponent>(ent.Owner))
            _popup.PopupEntity(Loc.GetString("ventriloquist-puppet-cant-speak"), ent.Owner, ent.Owner);
        else
            _popup.PopupEntity(Loc.GetString("speech-muted"), ent.Owner, ent.Owner);

        args.Args.Cancel();
    }
}
