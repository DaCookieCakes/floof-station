using System.Numerics;
using Content.Shared._Floof.MapLayer;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Floof.MapLayer;

public sealed partial class MapLayerSystem : SharedMapLayerSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    private static readonly EntProtoId EyeProto = "MapLayerEye";
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    private const float SendMargin = 1.3f;
    private const float KeepMargin = 1.5f;

    private readonly Dictionary<(ICommonSession Session, EntityUid Map), EntityUid> _eyes = new();
    private readonly HashSet<(ICommonSession Session, EntityUid Map)> _seen = new();
    private readonly List<(ICommonSession Session, EntityUid Map)> _unseen = new();
    private readonly List<(EntityUid Map, float Z)> _layers = new();
    private readonly List<(EntityUid Map, float Z)> _stackLayers = new();
    private TimeSpan _nextUpdate;

    [SubscribeLocalEvent]
    private void OnStackShutdown(Entity<MapLayerStackComponent> ent, ref ComponentShutdown args)
    {
        GetLayers(ent, _stackLayers, true);
        foreach (var (map, _) in _stackLayers)
        {
            if (!TerminatingOrDeleted(map))
                RemoveLayer(map);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateArrivals();

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        _seen.Clear();
        foreach (var session in _player.Sessions)
        {
            UpdatePlayer(session);
        }
        
        _unseen.Clear();
        foreach (var key in _eyes.Keys)
        {
            if (!_seen.Contains(key))
                _unseen.Add(key);
        }

        foreach (var key in _unseen)
        {
            RemoveEye(key);
        }
    }

    private void UpdatePlayer(ICommonSession session)
    {
        if (session.AttachedEntity is not { } player ||
            Transform(player).MapUid is not { } own ||
            !TryGetStack(player, out var stack) ||
            !TryGetZ(own, out var ownZ) ||
            !TryGetVisibility(player, out var belowRange, out var aboveFade))
            return;

        var position = _xform.GetWorldPosition(player);
        GetLayers(stack, _layers);
        foreach (var (map, z) in _layers)
        {
            if (map == own)
                continue;

            var key = (session, map);
            var margin = _eyes.ContainsKey(key) ? KeepMargin : SendMargin;
            var gap = ownZ - z;
            if (gap > 0f ? gap > belowRange * margin : -gap > aboveFade * margin)
                continue;

            _seen.Add(key);
            PlaceEye(key, position);
        }
    }

    private void PlaceEye((ICommonSession Session, EntityUid Map) key, Vector2 position)
    {
        var coordinates = new EntityCoordinates(key.Map, position);
        if (_eyes.TryGetValue(key, out var eye) && Exists(eye))
        {
            _xform.SetCoordinates(eye, coordinates);
            return;
        }

        eye = SpawnAtPosition(EyeProto, coordinates);
        Transform(eye).GridTraversal = false;
        _viewSubscriber.AddViewSubscriber(eye, key.Session);
        _eyes[key] = eye;
    }

    private void RemoveEye((ICommonSession Session, EntityUid Map) key)
    {
        if (!_eyes.Remove(key, out var eye))
            return;
        
        if (Exists(eye))
            QueueDel(eye);
    }
}
