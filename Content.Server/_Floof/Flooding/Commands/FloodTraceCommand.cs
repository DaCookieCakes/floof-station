using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Floof.Flooding.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FloodTraceCommand : LocalizedEntityCommands
{
    [Dependency] private FloodTraceSystem _trace = default!;

    public override string Command => "floodtrace";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-only-players-can-run-this-command"));
            return;
        }

        shell.WriteLine(Loc.GetString(_trace.Toggle(player, shell) ? "cmd-floodtrace-on" : "cmd-floodtrace-off"));
    }
}
