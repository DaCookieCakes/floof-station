using System.Numerics;
using Content.Server._Floof.MapLayer;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Station.Systems;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.Ocean;
using Content.Shared.Camera;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Arrivals;

public sealed partial class ArrivalsDiveSystem : EntitySystem
{
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapFluidSystem _fluid = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private ServerArrivalsSystem _arrivals = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;

    private const int LandingShakes = 10;
    private const float LandingShakeStrength = 0.6f;
    private const float CatchUpTime = 1f;
    private static readonly TimeSpan LandingShakeInterval = TimeSpan.FromSeconds(0.15);

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(MapLayerSystem));

        SubscribeLocalEvent<ArrivalsDiveComponent, ArrivalsTripEvent>(OnTrip);
        SubscribeLocalEvent<ArrivalsDiveComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ArrivalsDiveComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TransitionLayer is { } layer)
            _layers.FreeTransitionLayer(layer);
    }

    private void OnTrip(Entity<ArrivalsDiveComponent> ent, ref ArrivalsTripEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.TravelTime = ent.Comp.DiveTime;

        var layer = TakeTransitionLayer(ent);
        var now = _timing.CurTime;
        var stationMap = GetStationMap(args.Station);
        if (stationMap is { } fluidSource)
            _fluid.CopyFluid(layer, fluidSource);

        ent.Comp.Target = args.Target;
        ent.Comp.ToStation = args.ToStation;
        ent.Comp.ArriveTime = now + ent.Comp.DiveTime;

        _arrivals.TripDeparted(ent.Owner, args.Target, ent.Comp.DiveTime);

        _dock.UndockDocks(ent);
        var xform = Transform(ent);
        var (position, rotation) = _xform.GetWorldPositionRotation(xform);

        if (!DiveOverStation(ent, layer, stationMap, Transform(args.Terminal).MapUid, args, ref position, ref rotation))
            _layers.RemoveLayer(layer);

        _xform.SetCoordinates(ent, xform, new EntityCoordinates(layer, position), rotation);
        ent.Comp.Start = position;
        ent.Comp.Forward = rotation.RotateVec(Vector2.UnitY);
        _shuttles.Enable(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ArrivalsDiveComponent, ShuttleComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dive, out var shuttle, out var body))
        {
            if (dive.ShakesLeft > 0 && now >= dive.NextShake)
                Shake((uid, dive));

            if (dive.Target is not { } target)
                continue;

            if (now < dive.ArriveTime)
            {
                Sail((uid, dive, body), now);
                continue;
            }

            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            dive.Target = null;
            Arrive((uid, dive), shuttle, target);
        }
    }

    private void Sail(Entity<ArrivalsDiveComponent, PhysicsComponent> ent, TimeSpan now)
    {
        var dive = ent.Comp1;
        var progress = Math.Clamp(1f - (float) ((dive.ArriveTime - now).TotalSeconds / dive.DiveTime.TotalSeconds), 0f, 1f);
        var share = MathF.Min(1f, MathF.Min(progress / dive.SpeedUpShare, (1f - progress) / dive.SlowDownShare));
        var traveled = Vector2.Dot(_xform.GetWorldPosition(ent) - dive.Start, dive.Forward);
        var behind = GetPlannedDistance(dive, progress) - traveled;
        var speed = dive.Speed * Math.Clamp(share, 0f, 1f) + behind / CatchUpTime;
        _physics.SetLinearVelocity(ent, dive.Forward * speed, body: ent.Comp2);
        _physics.SetAngularVelocity(ent, 0f, body: ent.Comp2);
    }

    private static float GetPlannedDistance(ArrivalsDiveComponent dive, float progress)
    {
        var up = dive.SpeedUpShare;
        var down = dive.SlowDownShare;
        var shares = progress <= up
            ? progress * progress / (2f * up)
            : up / 2f + MathF.Min(progress, 1f - down) - up;

        if (progress > 1f - down)
        {
            var left = 1f - progress;
            shares += down / 2f - left * left / (2f * down);
        }

        return dive.Speed * (float) dive.DiveTime.TotalSeconds * shares;
    }

    private void Arrive(Entity<ArrivalsDiveComponent> ent, ShuttleComponent shuttle, EntityUid target)
    {
        if (Exists(target))
            _shuttles.TryFTLDock(ent, shuttle, target, ent.Comp.ToStation ? "DockArrivals" : null);

        if (ent.Comp.TransitionLayer is { } layer)
        {
            if (Transform(ent).MapUid == layer)
            {
                _layers.RemoveLayer(layer);
            }
            else
            {
                _layers.FreeTransitionLayer(layer);
                ent.Comp.TransitionLayer = null;
            }
        }

        if (ent.Comp.ToStation)
        {
            ent.Comp.ShakesLeft = LandingShakes;
            ent.Comp.NextShake = _timing.CurTime;
        }

        _arrivals.TripArrived(ent.Owner);
    }

    private void Shake(Entity<ArrivalsDiveComponent> ent)
    {
        var strength = LandingShakeStrength * ent.Comp.ShakesLeft / LandingShakes;
        ent.Comp.ShakesLeft--;
        ent.Comp.NextShake = _timing.CurTime + LandingShakeInterval;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } player && Transform(player).GridUid == ent.Owner)
                _recoil.KickCamera(player, _random.NextAngle().ToVec() * strength);
        }
    }

    private EntityUid TakeTransitionLayer(Entity<ArrivalsDiveComponent> ent)
    {
        if (ent.Comp.TransitionLayer is { } held && Exists(held))
            return held;

        var map = _layers.TakeTransitionLayer(ent);

        ent.Comp.TransitionLayer = map;
        return map;
    }

    private bool DiveOverStation(Entity<ArrivalsDiveComponent> ent,
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
            return _layers.StartTransition(layer, station, terminal, position, ent.Comp.DiveTime);

        if (_dock.GetDockingConfig(ent, args.Target, "DockArrivals") is { } config)
        {
            var dive = ent.Comp;
            var distance = GetPlannedDistance(dive, 1f);

            rotation = config.Angle + _xform.GetWorldRotation(config.Coordinates.EntityId);
            position = _xform.ToMapCoordinates(config.Coordinates).Position - rotation.RotateVec(Vector2.UnitY) * distance;
        }

        return _layers.StartTransition(layer, terminal, station, position, ent.Comp.DiveTime);
    }

    private EntityUid? GetStationMap(EntityUid station)
    {
        return _station.GetLargestGrid(station) is { } grid ? Transform(grid).MapUid : null;
    }
}
