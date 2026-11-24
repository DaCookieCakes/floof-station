using System.Numerics;
using Content.Server._Floof.MapLayer;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Systems;
using Content.Shared._Floof.Arrivals;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Ocean;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Arrivals;

public sealed partial class ArrivalsDiveSystem : EntitySystem
{
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MapFluidSystem _fluid = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private ServerArrivalsSystem _arrivals = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;

    private const float Speed = 3f;
    private const float SpeedUpShare = 0.05f;
    private const float SlowDownShare = 0.1f;

    private TimeSpan _diveTime;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(MapLayerSystem));

        Subs.CVar(_cfg, CCVars.ArrivalsDiveTime, value => _diveTime = TimeSpan.FromSeconds(value), true);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ArrivalsDiveComponent> ent, ref ComponentShutdown args)
    {
        if (TryGetPoolMap(ent, out var layer))
            _layers.FreeTransitionLayer(layer);
    }

    [SubscribeLocalEvent]
    private void OnTrip(Entity<ArrivalsDiveComponent> ent, ref ArrivalsTripEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.TravelTime = _diveTime;

        var layer = TryGetPoolMap(ent, out var held) ? held : _layers.TakeTransitionLayer(ent);
        var now = _timing.CurTime;
        var stationMap = GetStationMap(args.Station);
        if (stationMap is { } fluidSource)
            _fluid.CopyMapSettings(layer, fluidSource);

        var dive = EnsureComp<ArrivalsDivingComponent>(ent);
        dive.Target = args.Target;
        dive.ToStation = args.ToStation;
        dive.StartTime = now;
        dive.ArriveTime = now + _diveTime;

        _arrivals.TripDeparted(ent.Owner, args.Target, _diveTime);

        _dock.UndockDocks(ent);
        var xform = Transform(ent);
        var (position, rotation) = _xform.GetWorldPositionRotation(xform);

        if (!DiveOverStation(ent, layer, stationMap, Transform(args.Terminal).MapUid, args, ref position, ref rotation))
            _layers.RemoveLayer(layer);

        _xform.SetCoordinates(ent, xform, new EntityCoordinates(layer, position), rotation);
        dive.Start = position;
        dive.End = position + rotation.RotateVec(Vector2.UnitY) * GetDiveDistance();
        _shuttles.Disable(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ArrivalsDivingComponent, ShuttleComponent>();
        while (query.MoveNext(out var uid, out var dive, out var shuttle))
        {
            Move((uid, dive), now);

            if (now >= dive.ArriveTime)
                Arrive((uid, dive), shuttle);
        }
    }

    private void Move(Entity<ArrivalsDivingComponent> ent, TimeSpan now)
    {
        var dive = ent.Comp;
        var duration = dive.ArriveTime - dive.StartTime;
        var progress = duration > TimeSpan.Zero
            ? Math.Clamp((float) ((now - dive.StartTime) / duration), 0f, 1f)
            : 1f;

        var share = GetPlannedShare(progress) / GetPlannedShare(1f);
        _xform.SetWorldPosition(ent, Vector2.Lerp(dive.Start, dive.End, share));
    }

    private static float GetPlannedShare(float progress)
    {
        var shares = progress <= SpeedUpShare
            ? progress * progress / (2f * SpeedUpShare)
            : SpeedUpShare / 2f + MathF.Min(progress, 1f - SlowDownShare) - SpeedUpShare;

        if (progress > 1f - SlowDownShare)
        {
            var left = 1f - progress;
            shares += SlowDownShare / 2f - left * left / (2f * SlowDownShare);
        }

        return shares;
    }

    private float GetDiveDistance()
    {
        return Speed * (float) _diveTime.TotalSeconds * GetPlannedShare(1f);
    }

    private void Arrive(Entity<ArrivalsDivingComponent> ent, ShuttleComponent shuttle)
    {
        var target = ent.Comp.Target;
        var toStation = ent.Comp.ToStation;
        RemComp(ent, ent.Comp);

        var onLayer = TryGetPoolMap(ent, out var layer);
        var docked = Exists(target) && _shuttles.TryFTLDock(ent, shuttle, target, toStation ? "DockArrivals" : null);
        _shuttles.Enable(ent);

        if (onLayer)
        {
            if (Transform(ent).MapUid == layer)
                _layers.RemoveLayer(layer);
            else
                _layers.FreeTransitionLayer(layer);
        }

        if (docked && toStation)
            RaiseNetworkEvent(new ArrivalsLandedEvent(), Filter.BroadcastGrid(ent));

        _arrivals.TripArrived(ent.Owner);
    }

    private bool TryGetPoolMap(EntityUid uid, out EntityUid map)
    {
        map = Transform(uid).MapUid ?? EntityUid.Invalid;
        return HasComp<MapLayerPoolComponent>(map);
    }

    private bool DiveOverStation(EntityUid uid,
        EntityUid layer,
        EntityUid? stationMap,
        EntityUid? terminalMap,
        ArrivalsTripEvent args,
        ref Vector2 position,
        ref Angle rotation)
    {
        if (stationMap is not { } station ||
            terminalMap is not { } terminal ||
            !_layers.TryGetStack(station, out var stack) ||
            !_layers.TryGetStack(terminal, out var terminalStack) ||
            stack != terminalStack)
            return false;

        if (!args.ToStation)
            return _layers.StartTransition(layer, station, terminal, position, _diveTime);

        if (_dock.GetDockingConfig(uid, args.Target, "DockArrivals") is { } config)
        {
            rotation = config.Angle + _xform.GetWorldRotation(config.Coordinates.EntityId);
            position = _xform.ToMapCoordinates(config.Coordinates).Position - rotation.RotateVec(Vector2.UnitY) * GetDiveDistance();
        }

        return _layers.StartTransition(layer, terminal, station, position, _diveTime);
    }

    private EntityUid? GetStationMap(EntityUid station)
    {
        return _station.GetLargestGrid(station) is { } grid ? Transform(grid).MapUid : null;
    }
}
