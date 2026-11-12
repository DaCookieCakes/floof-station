using Content.Shared.Administration.Managers;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.Consent;

public sealed partial class ConsentExaminableSystem : EntitySystem
{
    [Dependency] private ISharedAdminManager _adminManager = default!;
    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConsentExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnExamineVerb);
    }

    private void OnExamineVerb(Entity<ConsentExaminableComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
            return;

        // Disabled for ghosts - you know who you are.
        var isGhost = HasComp<GhostComponent>(args.User) && !_adminManager.IsAdmin(args.User);
        var user = args.User;

        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                var markup = new FormattedMessage();
                markup.AddMarkupPermissive(ent.Comp.Content);
                _examine.SendExamineTooltip(user, ent, markup, false, false);
            },

            Text = Loc.GetString("game-hud-open-consent-menu-button-tooltip"),
            Category = VerbCategory.Examine,
            Disabled = isGhost,
            CloseMenu = true,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
        };

        args.Verbs.Add(verb);
    }
}
