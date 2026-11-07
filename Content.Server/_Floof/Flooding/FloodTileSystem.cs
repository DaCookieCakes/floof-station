using System.Numerics;
using Stopwatch = System.Diagnostics.Stopwatch;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Flooding;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared.Item;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Goes over the deep puddles every so often: liquid that doesn't burn puts out fires, and the current carries off
///     the loose items lying in it.
/// </summary>
public sealed partial class FloodTileSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FloodLevelSystem _level = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<ItemComponent> _itemQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private const float ScanInterval = 0.5f;

    /// <summary>
    ///     Current to item speed.
    /// </summary>
    private const float DriftScale = 2f;

    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2 Velocity)> _drifting = new();
    private readonly Stopwatch _stopwatch = new();

    private float _accumulator;
    private float _extinguishDepth;
    private float _sweepDepth;
    private float _driftCurrent;
    private float _driftSpeed;

    [ViewVariables]
    public int DriftingCount => _drifting.Count;

    [ViewVariables]
    public long ElapsedMicroseconds { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.FloodExtinguishDepth, value => _extinguishDepth = value, true);
        Subs.CVar(_cfg, CCVars.FloodSweepDepth, value => _sweepDepth = value, true);
        Subs.CVar(_cfg, CCVars.FloodItemDriftCurrent, value => _driftCurrent = value, true);
        Subs.CVar(_cfg, CCVars.FloodItemDriftSpeed, value => _driftSpeed = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator >= ScanInterval)
        {
            _accumulator -= ScanInterval;
            _stopwatch.Restart();
            Scan();
            ElapsedMicroseconds = _stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
        }

        // Tile friction would stop them between scans.
        foreach (var (uid, drift) in _drifting)
        {
            if (_xformQuery.TryComp(uid, out var xform) && xform.ParentUid == drift.Grid &&
                _physicsQuery.TryComp(uid, out var physics))
                _physics.SetLinearVelocity(uid, drift.Velocity, body: physics);
        }
    }

    private void Scan()
    {
        _drifting.Clear();

        var query = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var xform))
        {
            if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
                continue;

            var volume = _level.GetVolume(uid, puddle);
            var depth = FloodLevelSystem.VolumeToDepth(volume);
            if (depth < _extinguishDepth && depth < _sweepDepth)
                continue;

            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

            if (depth >= _extinguishDepth && _atmos.IsHotspotActive(gridUid, tile))
            {
                Entity<SolutionComponent>? solutionEnt = null;
                if (_solutions.ResolveSolution(uid, puddle.SolutionName, ref solutionEnt, out var solution) &&
                    solution.GetSolutionFlammability(_proto) <= 0)
                    _atmos.HotspotExtinguish(gridUid, tile);
            }

            // Items on a table only feel the water above its top.
            if (depth - _level.GetSurface(gridUid, grid, tile) >= _sweepDepth)
                CarryItems(gridUid, grid, tile, volume);
        }
    }

    private void CarryItems(EntityUid gridUid, MapGridComponent grid, Vector2i tile, float volume)
    {
        var current = _level.GetCurrent(gridUid, grid, tile, volume);
        var strength = current.Length();
        if (strength < _driftCurrent)
            return;

        var speed = MathF.Min(strength * DriftScale, _driftSpeed);
        var velocity = _transform.GetWorldRotation(gridUid).RotateVec(current / strength) * speed;

        foreach (var ent in _lookup.GetLocalEntitiesIntersecting(gridUid, tile, flags: LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (_itemQuery.HasComp(ent) &&
                _xformQuery.TryComp(ent, out var xform) && xform.ParentUid == gridUid && !xform.Anchored &&
                _physicsQuery.TryComp(ent, out var physics) && physics.BodyType == BodyType.Dynamic &&
                _map.TileIndicesFor(gridUid, grid, xform.Coordinates) == tile)
                _drifting[ent] = (gridUid, velocity);
        }
    }
}
