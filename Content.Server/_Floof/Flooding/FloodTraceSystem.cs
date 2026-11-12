using Content.Server._Floof.Swimming;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Fluids.Components;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Prints how hard the flood code is working to whoever asked for it, once a second.
/// </summary>
public sealed partial class FloodTraceSystem : EntitySystem
{
    [Dependency] private SwimmerMobSystem _mobs = default!;
    [Dependency] private FloodTileSystem _tiles = default!;
    [Dependency] private PuddleSystem _puddles = default!;

    private readonly Dictionary<ICommonSession, IConsoleShell> _listeners = new();

    private float _accumulator;
    private long _lastFlows;
    private long _lastRefreshes;
    private long _lastFlowMicroseconds;
    private long _lastRefreshMicroseconds;

    /// <returns>Whether the session is listening now.</returns>
    public bool Toggle(ICommonSession session, IConsoleShell shell)
    {
        if (_listeners.Remove(session))
            return false;

        _listeners[session] = shell;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_listeners.Count == 0)
            return;

        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator -= 1f;

        var line = $"puddles {Count<PuddleComponent>()}" +
                   $" | queued {_puddles.FlowQueueCount}, settled {_puddles.SettledPuddleCount}" +
                   $" | flows/s {_puddles.FlowCount - _lastFlows} in {(_puddles.FlowMicroseconds - _lastFlowMicroseconds) / 1000f:F2} ms" +
                   $" | refreshes/s {_puddles.RefreshCount - _lastRefreshes} in {(_puddles.RefreshMicroseconds - _lastRefreshMicroseconds) / 1000f:F2} ms" +
                   $" | mobs {_mobs.ElapsedMicroseconds / 1000f:F2} ms" +
                   $" | tile scan {_tiles.ElapsedMicroseconds / 1000f:F2} ms, items drifting {_tiles.DriftingCount}";

        _lastFlows = _puddles.FlowCount;
        _lastRefreshes = _puddles.RefreshCount;
        _lastFlowMicroseconds = _puddles.FlowMicroseconds;
        _lastRefreshMicroseconds = _puddles.RefreshMicroseconds;

        foreach (var (session, shell) in _listeners)
        {
            if (session.Status == SessionStatus.InGame)
                shell.WriteLine(line);
        }
    }
}
