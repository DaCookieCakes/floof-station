using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.MapLayer.Transition;
using Content.Shared._Floof.Ocean;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Floof.MapLayer.Transition;

public sealed partial class LayerTransitSystem : EntitySystem
{
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private MapFluidSystem _fluid = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private List<Entity<MapGridComponent>> _blocking = new();
    private readonly List<(EntityUid Map, float Z)> _stackLayers = new();

    private const float LiftHeight = 10f;

    public static readonly TimeSpan LiftTime = TimeSpan.FromSeconds(10);

    [SubscribeLocalEvent]
    private void OnLift(EntityUid uid, ShuttleConsoleComponent comp, SubLiftMessage args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } sub || xform.MapUid is not { } seabed)
            return;

        if (!HasComp<MapLayerSeabedComponent>(seabed) || !TryGetCruiseAbove(seabed, out var cruise))
            return;

        StartTransition(sub, seabed, cruise);
    }

    [SubscribeLocalEvent]
    private void OnLand(EntityUid uid, ShuttleConsoleComponent comp, SubLandMessage args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } sub ||
            xform.MapUid is not { } map ||
            !TryGetSeabedBelow(map, out _, out var seabed))
            return;

        if (IsBlocked(seabed, GetBounds(sub, 0f)))
        {
            _popup.PopupEntity(Loc.GetString("sub-helm-land-blocked"), uid, args.Actor);
            return;
        }

        if (StartTransition(sub, map, seabed) is not { } layer)
            return;

        _layers.AddPhantoms(layer, seabed, GetBounds(sub,  _cfg.GetCVar(CCVars.SubPhantomReach)));
    }

    private Box2Rotated GetBounds(EntityUid sub, float margin)
    {
        var (position, rotation) = _xform.GetWorldPositionRotation(sub);
        var local = Comp<MapGridComponent>(sub).LocalAABB.Enlarged(margin);
        return new Box2Rotated(local.Translated(position), rotation, position);
    }
    
    private bool IsBlocked(EntityUid map, Box2Rotated bounds)
    {
        _blocking.Clear();
        _map.FindGridsIntersecting(map, bounds, ref _blocking, includeMap: false);
        return _blocking.Count > 0;
    }
    
    [SubscribeLocalEvent]
    private void OnArrived(Entity<ShuttleComponent> ent, ref MapLayerArrivedEvent args)
    {
        if (!HasComp<MapLayerSeabedComponent>(args.Layer))
        {
            _shuttle.Enable(ent);
            return;
        }

        TryComp<DockApproachComponent>(ent, out var approach);
        if (approach != null)
            SnapToDock((ent, approach));

        Smimsh(ent);

        if (approach != null)
            ConnectDocks((ent, approach));
    }
    
    private EntityUid? StartTransition(EntityUid sub, EntityUid from, EntityUid to)
    {
        if (!_layers.TryGetZ(from, out var fromZ) || !_layers.TryGetZ(to, out var toZ))
            return null;

        var layer = _layers.TakeTransitionLayer(sub);
        _fluid.CopyFluid(layer, _fluid.HasFluid(from) ? from : to);

        var duration = GetDuration(fromZ, toZ);
        var subXform = Transform(sub);
        var (position, rotation) = _xform.GetWorldPositionRotation(subXform);
        if (!_layers.StartTransition(layer, from, to, position, duration))
        {
            _layers.FreeTransitionLayer(layer);
            return null;
        }

        Log.Info($"Moving {ToPrettyString(sub)} from Z {fromZ:0.0} to {toZ:0.0} through {ToPrettyString(layer)}");

        _shuttle.Enable(sub);
        _dock.UndockDocks(sub);
        _xform.SetCoordinates(sub, subXform, new EntityCoordinates(layer, position), rotation);
        return layer;
    }

    private static TimeSpan GetDuration(float fromZ, float toZ)
    {
        return LiftTime * (MathF.Max(MathF.Abs(toZ - fromZ), 0.1f) / LiftHeight);
    }

    private bool TryGetCruiseAbove(EntityUid seabed, out EntityUid cruise)
    {
        cruise = EntityUid.Invalid;
        if (!_layers.TryGetStack(seabed, out var stack) || !_layers.TryGetZ(seabed, out var z))
            return false;

        _layers.GetLayers(stack, _stackLayers);
        foreach (var (map, layerZ) in _stackLayers)
        {
            if (layerZ <= z || !HasComp<MapLayerCruiseComponent>(map))
                continue;

            cruise = map;
            return true;
        }

        return false;
    }
}
