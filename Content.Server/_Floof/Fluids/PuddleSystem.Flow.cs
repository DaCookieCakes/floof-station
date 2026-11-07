using Stopwatch = System.Diagnostics.Stopwatch;
using Content.Server._Floof.Flooding;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Spreader;
using Content.Shared._Floof.CCVars;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Spreader;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Fluids.EntitySystems;

/// <summary>
///     Overflowing puddles level out with their neighbours. Runs on its own queue instead of the spreader,
///     so the work is spread over the ticks and capped per tick.
/// </summary>
public sealed partial class PuddleSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpreaderSystem _spreader = default!;

    [Dependency] private EntityQuery<AirtightComponent> _airtightQuery = default!;
    [Dependency] private EntityQuery<DockingComponent> _dockingQuery = default!;

    private static readonly ProtoId<EdgeSpreaderPrototype> PuddleSpreader = "Puddle";

    /// <summary>
    ///     Two tiles closer than this share of the deeper one count as level, so a settled flood stops spreading.
    /// </summary>
    private const float LevelTolerance = 0.02f;

    /// <summary>
    ///     Most that two tiles can differ by and still count as level. A share alone would let the water drop by it on
    ///     every tile, so a long room would settle with its far end dry.
    /// </summary>
    private static readonly FixedPoint2 MaxLevelDifference = FixedPoint2.New(100);

    /// <summary>
    ///     Smallest difference that still flows, for shallow puddles.
    /// </summary>
    private static readonly FixedPoint2 FlowEpsilon = FixedPoint2.New(1);

    /// <summary>
    ///     A settled puddle only flows again once its volume moved this far (or by the level tolerance),
    ///     so evaporation doesn't wake it every tick.
    /// </summary>
    private static readonly FixedPoint2 WakeThreshold = FixedPoint2.New(10);

    private readonly Queue<EntityUid> _flowQueue = new();
    private readonly HashSet<EntityUid> _flowQueued = new();
    private readonly Dictionary<EntityUid, FixedPoint2> _settledVolume = new();
    private readonly Stopwatch _flowStopwatch = new();
    private readonly HashSet<EntityUid> _flowNow = new();

    // Flow moves liquid directly; slip, colour, reactions and so on catch up here, at most once per flow interval.
    private readonly Queue<(EntityUid Uid, TimeSpan Due)> _refreshQueue = new();
    private readonly Dictionary<EntityUid, bool> _refreshPending = new();
    private readonly Dictionary<EntityUid, FixedPoint2> _refreshedVolume = new();

    [ViewVariables]
    public int FlowQueueCount => _flowQueue.Count;

    [ViewVariables]
    public int SettledPuddleCount => _settledVolume.Count;

    [ViewVariables]
    public long FlowCount { get; private set; }

    [ViewVariables]
    public long RefreshCount { get; private set; }

    [ViewVariables]
    public long FlowMicroseconds { get; private set; }

    [ViewVariables]
    public long RefreshMicroseconds { get; private set; }

    private float _flowInterval;
    private float _maxFlowTime;
    private float _flowCredit;

    private enum FlowKind : byte
    {
        Puddle,
        FreeTile,
        Sink,
    }

    private record struct FlowTarget(FlowKind Kind, FixedPoint2 Volume, EntityUid Puddle, Entity<SolutionComponent>? Solution, TileRef Tile);

    private void InitializeFlow()
    {
        SubscribeLocalEvent<PuddleComponent, SpreadNeighborsEvent>(OnPuddleSpread);
        SubscribeLocalEvent<PuddleComponent, ComponentShutdown>(OnPuddleShutdown);
        SubscribeLocalEvent<AirtightChanged>(OnAirtightChanged);

        Subs.CVar(_cfg, CCVars.FloodFlowInterval, value => _flowInterval = value, true);
        Subs.CVar(_cfg, CCVars.FloodMaxProcessTime, value => _maxFlowTime = value, true);
    }

    /// <summary>
    ///     Queues the puddle to flow, unless it's already waiting.
    /// </summary>
    public void WakePuddle(EntityUid uid)
    {
        _settledVolume.Remove(uid);
        if (_flowQueued.Add(uid))
            _flowQueue.Enqueue(uid);
    }

    protected override void OnPuddleChanged(Entity<PuddleComponent> entity, Solution solution)
    {
        _refreshedVolume[entity] = solution.Volume;
        WakeIfMoved(entity, solution.Volume);
        var ev = new PuddleVolumeChangedEvent(solution.Volume);
        RaiseLocalEvent(entity, ref ev);
    }

    protected override void OnPuddleEvaporated(Entity<SolutionComponent> solution, EntityUid puddle)
    {
        if (solution.Comp.Solution.Volume > FixedPoint2.Zero)
            QueueRefresh(puddle, false);
        else
            base.OnPuddleEvaporated(solution, puddle);
    }

    private static FixedPoint2 GetLevelEpsilon(FixedPoint2 level)
    {
        return FixedPoint2.Max(FlowEpsilon, FixedPoint2.Min(level * LevelTolerance, MaxLevelDifference));
    }

    private void WakeIfMoved(EntityUid uid, FixedPoint2 volume)
    {
        if (_settledVolume.TryGetValue(uid, out var settled) &&
            FixedPoint2.Abs(volume - settled) < FixedPoint2.Max(WakeThreshold, GetLevelEpsilon(settled)))
            return;

        WakePuddle(uid);
    }

    /// <param name="mixing">The composition changed, so it can react and needs a full update.</param>
    private void QueueRefresh(EntityUid uid, bool mixing)
    {
        if (_refreshPending.TryGetValue(uid, out var pending))
        {
            _refreshPending[uid] = pending || mixing;
            return;
        }

        _refreshPending[uid] = mixing;
        _refreshQueue.Enqueue((uid, _timing.CurTime + TimeSpan.FromSeconds(_flowInterval)));
    }

    private static bool IsMixing(Solution target, Solution added)
    {
        return target.Contents.Count != 1 || added.Contents.Count != 1 ||
               target.Contents[0].Reagent.Prototype != added.Contents[0].Reagent.Prototype;
    }

    private void ProcessRefreshes()
    {
        var curTime = _timing.CurTime;
        while (_refreshQueue.TryPeek(out var next) && next.Due <= curTime &&
               _flowStopwatch.Elapsed.TotalMilliseconds < _maxFlowTime)
        {
            _refreshQueue.Dequeue();
            if (_refreshPending.Remove(next.Uid, out var mixing) && _puddleQuery.TryComp(next.Uid, out var puddle))
                Refresh((next.Uid, puddle), mixing);
        }
    }

    private void Refresh(Entity<PuddleComponent> entity, bool mixing)
    {
        if (TerminatingOrDeleted(entity) ||
            !_solutionContainerSystem.ResolveSolution(entity.Owner, entity.Comp.SolutionName, ref entity.Comp.Solution,
                out var solution))
            return;

        // Same liquid and barely any volume change: nothing anyone could see.
        if (!mixing && _refreshedVolume.TryGetValue(entity, out var refreshed) &&
            FixedPoint2.Abs(solution.Volume - refreshed) < FixedPoint2.Max(FlowEpsilon, refreshed * LevelTolerance))
            return;

        RefreshCount++;
        _solutionContainerSystem.UpdateChemicals(entity.Comp.Solution.Value, mixing);
    }

    // The spreader still wakes puddles next to opened doors and removed walls.
    private void OnPuddleSpread(Entity<PuddleComponent> entity, ref SpreadNeighborsEvent args)
    {
        RemCompDeferred<ActiveEdgeSpreaderComponent>(entity);
        WakePuddle(entity);
    }

    /// <summary>
    ///     An opening door shouldn't wait for the spreader and the queue: the liquid behind it moves on the next tick.
    /// </summary>
    private void OnAirtightChanged(ref AirtightChanged ev)
    {
        if (!ev.AirBlockedChanged || ev.Airtight.AirBlocked || !TryComp<MapGridComponent>(ev.Position.Grid, out var grid))
            return;

        var (gridUid, tile) = ev.Position;
        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var anchored = _map.GetAnchoredEntities(gridUid, grid, tile.Offset((AtmosDirection) (1 << i)));
            while (anchored.MoveNext(out var ent))
            {
                if (_puddleQuery.HasComp(ent))
                    _flowNow.Add(ent.Value);
            }
        }
    }

    private void OnPuddleShutdown(Entity<PuddleComponent> entity, ref ComponentShutdown args)
    {
        _flowNow.Remove(entity);
        _flowQueued.Remove(entity);
        _settledVolume.Remove(entity);
        _refreshPending.Remove(entity);
        _refreshedVolume.Remove(entity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _flowStopwatch.Restart();
        ProcessRefreshes();
        var refreshTicks = _flowStopwatch.ElapsedTicks;
        RefreshMicroseconds += refreshTicks * 1_000_000 / Stopwatch.Frequency;

        foreach (var uid in _flowNow)
        {
            if (_puddleQuery.TryComp(uid, out var puddle) && !TerminatingOrDeleted(uid) && Flow((uid, puddle)))
                WakePuddle(uid);
        }

        _flowNow.Clear();

        if (_flowQueue.Count == 0)
        {
            _flowCredit = 0;
            return;
        }

        // Every queued puddle flows about once per interval, a slice of them each tick.
        var pending = _flowQueue.Count;
        _flowCredit = MathF.Min(_flowCredit + pending * frameTime / MathF.Max(_flowInterval, frameTime), pending);

        while (_flowCredit >= 1 && pending-- > 0 && _flowQueue.TryDequeue(out var uid))
        {
            if (!_flowQueued.Remove(uid))
                continue;

            _flowCredit--;
            FlowCount++;
            if (_puddleQuery.TryComp(uid, out var puddle) && !TerminatingOrDeleted(uid) && Flow((uid, puddle)))
                WakePuddle(uid);

            if (_flowStopwatch.Elapsed.TotalMilliseconds >= _maxFlowTime)
                break;
        }

        FlowMicroseconds += (_flowStopwatch.ElapsedTicks - refreshTicks) * 1_000_000 / Stopwatch.Frequency;
    }

    /// <summary>
    ///     Liquid above <see cref="PuddleComponent.OverflowVolume"/> levels out with every lower neighbour:
    ///     puddles, dry floor and space or empty tiles, which drain it. Higher neighbours get woken to flow in.
    ///     Returns whether it should flow again.
    /// </summary>
    private bool Flow(Entity<PuddleComponent> entity)
    {
        if (!_solutionContainerSystem.ResolveSolution(entity.Owner, entity.Comp.SolutionName, ref entity.Comp.Solution,
                out var solution))
            return false;

        var xform = Transform(entity);
        if (xform.GridUid == null)
            return false;

        _spreader.GetNeighbors(entity, xform, PuddleSpreader, out var freeTiles, out _, out var neighbors);

        var level = solution.Volume;
        var epsilon = GetLevelEpsilon(level);
        var targets = new ValueList<FlowTarget>();

        foreach (var neighbor in neighbors)
        {
            if (!_puddleQuery.TryGetComponent(neighbor, out var puddle) ||
                !_solutionContainerSystem.ResolveSolution(neighbor, puddle.SolutionName, ref puddle.Solution,
                    out var neighborSolution))
            {
                continue;
            }

            var volume = neighborSolution.Volume;
            if (volume + epsilon < level)
                targets.Add(new FlowTarget(FlowKind.Puddle, volume, neighbor, puddle.Solution, default));
            else if (volume > level + GetLevelEpsilon(volume) && volume > puddle.OverflowVolume)
                WakePuddle(neighbor);
        }

        if (level <= entity.Comp.OverflowVolume)
        {
            _settledVolume[entity] = level;
            return false;
        }

        foreach (var (_, tile) in freeTiles)
        {
            targets.Add(new FlowTarget(FlowKind.FreeTile, FixedPoint2.Zero, EntityUid.Invalid, null, tile));
        }

        AddEmptySinks(entity, xform, ref targets);

        // Water level shared with every neighbour below it, never under the surface tension.
        targets.Sort((x, y) => x.Volume.CompareTo(y.Volume));
        var total = level;
        var count = 1;
        foreach (var target in targets)
        {
            if (target.Volume * count >= total)
                break;

            total += target.Volume;
            count++;
        }

        var shared = FixedPoint2.Max(total / count, entity.Comp.OverflowVolume);
        var available = level - shared;
        if (count == 1 || available <= FixedPoint2.Zero)
        {
            _settledVolume[entity] = level;
            return false;
        }

        var wanted = FixedPoint2.Zero;
        for (var i = 0; i < count - 1; i++)
        {
            wanted += shared - targets[i].Volume;
        }

        var scale = wanted > available ? available.Float() / wanted.Float() : 1f;
        var outflow = solution.SplitSolution(available);
        QueueRefresh(entity, false);

        for (var i = 0; i < count - 1; i++)
        {
            var target = targets[i];
            var split = outflow.SplitSolution((shared - target.Volume) * scale);
            if (split.Volume <= FixedPoint2.Zero)
                continue;

            switch (target.Kind)
            {
                case FlowKind.Puddle:
                    var targetSolution = target.Solution!.Value.Comp.Solution;
                    QueueRefresh(target.Puddle, IsMixing(targetSolution, split));
                    targetSolution.AddSolution(split, ProtoMan);
                    WakeIfMoved(target.Puddle, targetSolution.Volume);
                    break;
                case FlowKind.FreeTile:
                    TrySpillAt(target.Tile, split, out _, false);
                    break;
            }
        }

        if (outflow.Volume > FixedPoint2.Zero)
            solution.AddSolution(outflow, ProtoMan);

        return true;
    }

    /// <summary>
    ///     Tiles next to this one with no floor at all. The spreader never lists them, but liquid pours off the edge.
    /// </summary>
    private void AddEmptySinks(EntityUid uid, TransformComponent xform, ref ValueList<FlowTarget> targets)
    {
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        var blocked = AtmosDirection.Invalid;
        var anchored = _map.GetAnchoredEntities(xform.GridUid.Value, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            // Docked airlocks lead onto the other grid, not into space.
            if (_dockingQuery.TryComp(ent, out var dock) && dock.Docked)
                return;

            if (_airtightQuery.TryComp(ent, out var airtight) && airtight.AirBlocked)
                blocked |= airtight.AirBlockedDirection;
        }

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var dir = (AtmosDirection) (1 << i);
            if ((blocked & dir) != 0)
                continue;

            var neighbor = tile.Offset(dir);
            if (_map.TryGetTileRef(xform.GridUid.Value, grid, neighbor, out var tileRef) && !tileRef.Tile.IsEmpty)
                continue;

            targets.Add(new FlowTarget(FlowKind.Sink, FixedPoint2.Zero, EntityUid.Invalid, null, tileRef));
        }
    }
}
