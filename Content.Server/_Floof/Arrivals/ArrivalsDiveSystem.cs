using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Station.Systems;
using Content.Shared._Floof.DayNightCycle;
using Content.Shared._Floof.MapUnderlay;
using Content.Shared._Floof.Ocean;
using Content.Shared.Camera;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Arrivals;

/// <summary>
/// Takes a diving arrivals shuttle down to the station and back up to the terminal. It sails through a map of its own,
/// really moving, then docks at the other end.
/// </summary>
public sealed partial class ArrivalsDiveSystem : EntitySystem
{
    [Dependency] private DayNightCycleSystem _dayNight = default!;
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private ServerArrivalsSystem _arrivals = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;

    private const int LandingShakes = 10;
    private const float LandingShakeStrength = 0.6f;
    private static readonly TimeSpan LandingShakeInterval = TimeSpan.FromSeconds(0.15);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArrivalsDiveComponent, ArrivalsTripEvent>(OnTrip);
        SubscribeLocalEvent<ArrivalsDiveComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ArrivalsDiveComponent> ent, ref ComponentShutdown args)
    {
        QueueDel(ent.Comp.TravelMap);
    }

    private void OnTrip(Entity<ArrivalsDiveComponent> ent, ref ArrivalsTripEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.TravelTime = ent.Comp.DiveTime;

        var travel = EnsureTravelMap(ent);
        var depth = EnsureComp<OceanDepthComponent>(travel);
        var now = _timing.CurTime;
        // As deep as the station's sea floor, if it has one.
        var stationMap = GetStationMap(args.Station);
        var floor = CompOrNull<OceanDepthComponent>(stationMap);
        if (floor != null)
            depth.FloorDepth = floor.FloorDepth;
        depth.StartDepth = args.ToStation ? 0f : depth.FloorDepth;
        depth.Rate = (args.ToStation ? 1f : -1f) * depth.FloorDepth / (float) ent.Comp.DiveTime.TotalSeconds;
        depth.StartTime = now;
        depth.SurfaceLight = GetSurfaceLight(args.Terminal);
        Dirty(travel, depth);

        ent.Comp.Target = args.Target;
        ent.Comp.ToStation = args.ToStation;
        ent.Comp.ArriveTime = now + ent.Comp.DiveTime;

        _arrivals.TripDeparted(ent.Owner, args.Target, ent.Comp.DiveTime);

        // Same spot as at the terminal, so the ripples carry on where they were.
        _dock.UndockDocks(ent);
        var xform = Transform(ent);
        var (position, rotation) = _xform.GetWorldPositionRotation(xform);

        RemComp<MapUnderlayComponent>(travel);
        if (floor is { Seabed: true })
            ShowStationBelow(ent, travel, stationMap!.Value, args, position, rotation);

        _xform.SetCoordinates(ent, xform, new EntityCoordinates(travel, position), rotation);
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

    /// <summary>
    /// Holds the speed the trip calls for right now, which also keeps tile friction from slowing it.
    /// </summary>
    private void Sail(Entity<ArrivalsDiveComponent, PhysicsComponent> ent, TimeSpan now)
    {
        var dive = ent.Comp1;
        var progress = 1f - (float) ((dive.ArriveTime - now).TotalSeconds / dive.DiveTime.TotalSeconds);
        var share = MathF.Min(1f, MathF.Min(progress / dive.SpeedUpShare, (1f - progress) / dive.SlowDownShare));
        var forward = _xform.GetWorldRotation(ent).RotateVec(Vector2.UnitY);
        _physics.SetLinearVelocity(ent, forward * dive.Speed * Math.Clamp(share, 0f, 1f), body: ent.Comp2);
        _physics.SetAngularVelocity(ent, 0f, body: ent.Comp2);
    }

    private void Arrive(Entity<ArrivalsDiveComponent> ent, ShuttleComponent shuttle, EntityUid target)
    {
        if (ent.Comp.TravelMap is { } travel)
            RemComp<MapUnderlayComponent>(travel);

        if (Exists(target))
            _shuttles.TryFTLDock(ent, shuttle, target, ent.Comp.ToStation ? "DockArrivals" : null);

        // Whoever got off on the way stays out there.
        if (ent.Comp.ToStation)
        {
            ent.Comp.ShakesLeft = LandingShakes;
            ent.Comp.NextShake = _timing.CurTime;
        }

        _arrivals.TripArrived(ent.Owner);
    }

    /// <summary>
    /// One hard jolt that dies down, for everyone aboard.
    /// </summary>
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

    private EntityUid EnsureTravelMap(Entity<ArrivalsDiveComponent> ent)
    {
        if (ent.Comp.TravelMap is { } existing && Exists(existing))
            return existing;

        var map = _map.CreateMap();
        EnsureComp<OceanDepthComponent>(map);
        EnsureComp<OceanSeaComponent>(map);
        EnsureComp<MapLightComponent>(map);
        var gravity = EnsureComp<GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(map, gravity);
        _metaData.SetEntityName(map, Name(ent));

        ent.Comp.TravelMap = map;
        return map;
    }

    /// <summary>
    ///     Shows the station's sea floor beneath the travel map, lined up so the shuttle comes down right onto its dock,
    ///     or rises away from where it left it.
    /// </summary>
    private void ShowStationBelow(Entity<ArrivalsDiveComponent> ent,
        EntityUid travel,
        EntityUid stationMap,
        ArrivalsTripEvent args,
        Vector2 position,
        Angle rotation)
    {
        var underlay = EnsureComp<MapUnderlayComponent>(travel);
        underlay.Map = stationMap;
        underlay.Offset = Vector2.Zero;
        underlay.Rotation = Angle.Zero;

        // Leaving, it starts right where it was docked.
        if (args.ToStation && _dock.GetDockingConfig(ent, args.Target, "DockArrivals") is { } config)
        {
            // Where sailing ends: full speed for the whole trip, less half the time spent speeding up and slowing down.
            var dive = ent.Comp;
            var distance = dive.Speed * (float) dive.DiveTime.TotalSeconds * (1f - (dive.SpeedUpShare + dive.SlowDownShare) / 2f);
            var end = position + rotation.RotateVec(Vector2.UnitY) * distance;

            var dock = _xform.ToMapCoordinates(config.Coordinates).Position;
            var dockRotation = config.Angle + _xform.GetWorldRotation(config.Coordinates.EntityId);
            underlay.Rotation = dockRotation - rotation;
            underlay.Offset = dock - underlay.Rotation.RotateVec(end);
        }

        Dirty(travel, underlay);
    }

    private EntityUid? GetStationMap(EntityUid station)
    {
        return _station.GetLargestGrid(station) is { } grid ? Transform(grid).MapUid : null;
    }

    /// <summary>
    /// The daylight at the terminal, so the water at the top looks like the water the shuttle just left.
    /// </summary>
    private Color GetSurfaceLight(EntityUid terminal)
    {
        if (Transform(terminal).MapUid is not { } map)
            return Color.Black;

        if (TryComp<DayNightCycleComponent>(map, out var cycle))
            return _dayNight.GetColor((map, cycle));

        return CompOrNull<MapLightComponent>(map)?.AmbientLightColor ?? Color.Black;
    }
}
