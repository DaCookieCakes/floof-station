using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Ghost;

public sealed partial class GhostRespawnSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ServerGameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<GhostRespawnComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<GhostRespawnRequestEvent>(OnRespawnRequest);
    }

    /// <summary>
    ///     Records the death on the mind, so ghosts take their time of death from the death instead of from ghosting.
    /// </summary>
    private void OnMobStateChanged(EntityUid uid, MindContainerComponent comp, MobStateChangedEvent args)
    {
        if (!_mind.TryGetMind(uid, out _, out var mind, comp))
            return;

        if (args.NewMobState == MobState.Dead)
            mind.TimeOfDeath ??= _timing.RealTime;
        else
            mind.TimeOfDeath = null;
    }

    /// <summary>
    ///     Starts the timer at the time of death. Admins skip it when they ask to respawn.
    /// </summary>
    private void OnPlayerAttached(EntityUid uid, GhostRespawnComponent comp, PlayerAttachedEvent args)
    {
        if (comp.RespawnTime != null || !TryComp<GhostComponent>(uid, out var ghost))
            return;

        var delay = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.GhostRespawnTime));
        var dead = _timing.RealTime - ghost.TimeOfDeath;
        comp.RespawnTime = _timing.CurTime + (delay > dead ? delay - dead : TimeSpan.Zero);
        Dirty(uid, comp);
    }

    private void OnRespawnRequest(GhostRespawnRequestEvent msg, EntitySessionEventArgs args)
    {
        if (!TryComp<GhostRespawnComponent>(args.SenderSession.AttachedEntity, out var respawn)
            || respawn.RespawnTime is not { } time
            || _timing.CurTime < time && !_admin.IsAdmin(args.SenderSession))
            return;

        _ticker.Respawn(args.SenderSession);
    }
}
