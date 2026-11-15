using System.Linq;
using Content.Server._Floof.Persistence;
using Content.Server.Preferences.Managers;
using Content.Shared._Floof.Consent;
using Content.Shared._Floof.Consent.Systems;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Components;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.Consent.Systems;

public sealed partial class ConsentSystem : SharedConsentSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ConsentUpdateRequestEvent>(OnUpdateRequest);
    }

    /// <summary>
    ///     Applies the in-round consent window to the player's body, and to its character slot if it is one of theirs.
    /// </summary>
    private async void OnUpdateRequest(ConsentUpdateRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || HasComp<GhostComponent>(uid))
            return;

        var text = HumanoidCharacterProfile.SanitizeConsentText(msg.Text, _cfg.GetCVar(CCVars.MaxConsentTextLength));
        var toggles = msg.Toggles.Where(t => _prototype.HasIndex(t)).ToHashSet();

        if (text != "")
        {
            var examinable = EnsureComp<ConsentExaminableComponent>(uid);
            examinable.Content = text;
            Dirty(uid, examinable);
        }
        else
            RemComp<ConsentExaminableComponent>(uid);

        if (toggles.Count > 0)
        {
            var comp = EnsureComp<ConsentTogglesComponent>(uid);
            comp.Toggles = toggles;
            Dirty(uid, comp);
        }
        else
            RemComp<ConsentTogglesComponent>(uid);

        if (!TryComp<LiveCharacterComponent>(uid, out var live)
            || live.User != args.SenderSession.UserId
            || !_prefs.TryGetCachedPreferences(live.User, out var prefs)
            || !prefs.Characters.TryGetValue(live.Slot, out var profile))
            return;

        await _prefs.SetProfile(live.User, live.Slot, profile.WithConsentText(text).WithConsentToggles(toggles), select: false);
        _prefs.SendPreferences(args.SenderSession);
    }
}
