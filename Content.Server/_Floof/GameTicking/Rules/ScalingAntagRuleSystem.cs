using System.Linq;
using Content.Server._Floof.GameTicking.Rules.Components;
using Content.Shared.GameTicking;
using Content.Shared.Antag;
using Content.Shared.Antag.Components;
using Content.Shared.GameTicking.Rules;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Floof.GameTicking.Rules;

public sealed partial class ScalingAntagRuleSystem : GameRuleSystem<ScalingAntagRuleComponent>
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Roundstart spawns don't count, the round always starts at zero.
        if (args.LateJoin)
            UpdateAll();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus is SessionStatus.InGame or SessionStatus.Disconnected)
            UpdateAll();
    }

    private void UpdateAll()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out var scaling, out _, out _))
        {
            if (TryComp<AntagSelectionComponent>(uid, out var selection))
                Update((uid, selection), scaling);
        }
    }

    private void Update(Entity<AntagSelectionComponent> rule, ScalingAntagRuleComponent scaling)
    {
        if (!_proto.Resolve(scaling.Antag, out var def))
            return;

        var players = _antag.GetActivePlayers().ToList();
        var target = Math.Min(players.Count / Math.Max(scaling.PlayerRatio, 1), scaling.Max);
        var missing = target - _antag.GetAssignedAntagCount(rule, scaling.Antag);
        if (missing <= 0)
            return;

        RobustRandom.Shuffle(players);
        foreach (var session in players)
        {
            if (missing <= 0)
                break;

            if (_antag.TryMakeAntag(rule, def, session))
                missing--;
        }
    }
}
