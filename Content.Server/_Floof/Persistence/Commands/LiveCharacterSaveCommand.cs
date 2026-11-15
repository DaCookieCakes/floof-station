using Content.Server.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Floof.Persistence.Commands;

/// <summary>
///     Saves a player's in-round body as their live character, to test persistence without ending the round.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class LiveCharacterSaveCommand : LocalizedEntityCommands
{
    [Dependency] private LiveCharacterManager _liveCharacters = default!;
    [Dependency] private LiveCharacterSystem _liveCharacter = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private GameTicker _ticker = default!;

    public override string Command => "livecharsave";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        ICommonSession? player;
        if (args.Length == 1)
        {
            if (!_players.TryGetSessionByUsername(args[0], out player))
            {
                shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
                return;
            }
        }
        else if ((player = shell.Player) == null)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (!_liveCharacter.TryGetOwnBody(player, out var body))
        {
            shell.WriteError(Loc.GetString("cmd-livecharsave-no-body", ("player", player.Name)));
            return;
        }

        if (!_liveCharacter.TryCreateRecord(body, out var record))
        {
            shell.WriteError(Loc.GetString("cmd-livecharsave-failed", ("player", player.Name), ("slot", body.Comp.Slot)));
            return;
        }

        var slot = body.Comp.Slot;
        if (!await _liveCharacters.Save(player.UserId, slot, record, _ticker.RoundId))
        {
            shell.WriteError(Loc.GetString("cmd-livecharsave-failed", ("player", player.Name), ("slot", slot)));
            return;
        }

        _liveCharacter.SendLive(player);
        shell.WriteLine(Loc.GetString("cmd-livecharsave-saved", ("player", player.Name), ("slot", slot)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), Loc.GetString("cmd-livecharsave-arg-player"))
            : CompletionResult.Empty;
    }
}
