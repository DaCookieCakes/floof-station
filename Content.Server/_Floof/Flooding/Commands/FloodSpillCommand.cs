using System.Globalization;
using Content.Server.Administration;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Floof.Flooding;
using Content.Shared.Administration;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Floof.Flooding.Commands;

/// <summary>
///     Spills at your feet the normal way. A tile only holds so much, so the rest goes in as the liquid flows away,
///     until it's all out or the tile stayed full for several tries.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed partial class FloodSpillCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan SpillInterval = TimeSpan.FromSeconds(0.5);
    private const int MaxFailedTries = 20;

    public override string Command => "floodspill";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-must-be-attached-to-entity"));
            return;
        }

        if (!_proto.HasIndex<ReagentPrototype>(args[0]))
        {
            shell.WriteError(Loc.GetString("cmd-floodspill-bad-reagent", ("reagent", args[0])));
            return;
        }

        if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        var coords = EntityManager.GetComponent<TransformComponent>(player).Coordinates;
        SpillStep(shell, coords, args[0], amount, amount, 0);
    }

    private void SpillStep(IConsoleShell shell, EntityCoordinates coords, string reagent, FixedPoint2 total, FixedPoint2 remaining, int failed)
    {
        if (_transform.GetGrid(coords) is not { } grid || !EntityManager.TryGetComponent<MapGridComponent>(grid, out var gridComp))
        {
            Finish(shell, total, remaining);
            return;
        }

        var tile = _map.GetTileRef(grid, gridComp, coords);
        var amount = FixedPoint2.Min(remaining, GetFreeVolume(tile));

        if (amount > FixedPoint2.Zero && _puddle.TrySpillAt(tile, new Solution(reagent, amount), out _, false))
        {
            remaining -= amount;
            failed = 0;
        }
        else if (++failed >= MaxFailedTries)
        {
            Finish(shell, total, remaining);
            return;
        }

        if (remaining <= FixedPoint2.Zero)
        {
            Finish(shell, total, remaining);
            return;
        }

        var (left, tries) = (remaining, failed);
        Timer.Spawn(SpillInterval, () => SpillStep(shell, coords, reagent, total, left, tries));
    }

    private FixedPoint2 GetFreeVolume(TileRef tile)
    {
        if (!_puddle.TryGetPuddle(tile, out var puddle) ||
            !EntityManager.TryGetComponent<PuddleComponent>(puddle, out var comp) ||
            !_solution.ResolveSolution(puddle, comp.SolutionName, ref comp.Solution, out var solution))
        {
            return FloodConstants.TileVolume;
        }

        return solution.AvailableVolume;
    }

    private void Finish(IConsoleShell shell, FixedPoint2 total, FixedPoint2 remaining)
    {
        shell.WriteLine(Loc.GetString("cmd-floodspill-done", ("spilled", total - remaining), ("total", total)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<ReagentPrototype>(), Loc.GetString("cmd-floodspill-arg-reagent")),
            2 => CompletionResult.FromHint(Loc.GetString("cmd-floodspill-arg-amount")),
            _ => CompletionResult.Empty,
        };
    }
}
