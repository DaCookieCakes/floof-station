using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind.Components;
using Content.Shared._Floof.MapLayer;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Floof.MapLayer;

public sealed partial class MapLayerSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private readonly List<(EntityUid Layer, EntityUid Destination)> _arrived = new();
    private readonly List<EntityUid> _grids = new();
    private readonly List<EntityUid> _phantoms = new();

    private void UpdateArrivals()
    {
        var now = _timing.CurTime;
        _arrived.Clear();
        var query = EntityQueryEnumerator<MapLayerPoolComponent, MapLayerTransitionComponent, MapLayerComponent>();
        while (query.MoveNext(out var uid, out var pooled, out var transition, out var layer))
        {
            if (pooled.InUse &&
                now >= transition.StartTime + transition.Duration &&
                FindLayerAt(layer.Stack, transition.EndZ) is { } destination)
                _arrived.Add((uid, destination));
        }

        foreach (var (layer, destination) in _arrived)
        {
            ArriveOn(layer, destination);
        }
    }

    private EntityUid? FindLayerAt(EntityUid stack, float z)
    {
        var query = EntityQueryEnumerator<MapLayerComponent>();
        while (query.MoveNext(out var uid, out var layer))
        {
            if (layer.Stack == stack &&
                !layer.NoOp &&
                MathHelper.CloseTo(layer.Z, z) &&
                !HasComp<MapLayerTransitionComponent>(uid))
                return uid;
        }

        return null;
    }

    private void ArriveOn(EntityUid layer, EntityUid destination)
    {
        _grids.Clear();
        _phantoms.Clear();
        var children = Transform(layer).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<PhantomGridComponent>(child))
                _phantoms.Add(child);
            else if (HasComp<MapGridComponent>(child))
                _grids.Add(child);
        }

        foreach (var phantom in _phantoms)
        {
            Del(phantom);
        }

        foreach (var grid in _grids)
        {
            var xform = Transform(grid);
            var (position, rotation) = _xform.GetWorldPositionRotation(xform);
            _xform.SetCoordinates(grid, xform, new EntityCoordinates(destination, position), rotation);
            Log.Info($"{ToPrettyString(grid)} arrived on {ToPrettyString(destination)}");

            var ev = new MapLayerArrivedEvent(destination);
            RaiseLocalEvent(grid, ref ev);
        }

        FreeTransitionLayer(layer);
    }

    [PublicAPI]
    public EntityUid TakeTransitionLayer(EntityUid user)
    {
        var map = FindFreeTransitionLayer() ?? _map.CreateMap();
        EnsureComp<MapLayerPoolComponent>(map).InUse = true;
        _metaData.SetEntityName(map, Loc.GetString("map-layer-transition-in-use", ("user", Name(user))));
        return map;
    }

    private EntityUid? FindFreeTransitionLayer()
    {
        var query = EntityQueryEnumerator<MapLayerPoolComponent>();
        while (query.MoveNext(out var uid, out var pooled))
        {
            if (!pooled.InUse)
                return uid;
        }

        return null;
    }

    [PublicAPI]
    public void FreeTransitionLayer(EntityUid map)
    {
        if (!TryComp<MapLayerPoolComponent>(map, out var pooled))
            return;

        SweepLostAtSea(map);
        RemoveLayer(map);
        pooled.InUse = false;
        _metaData.SetEntityName(map, Loc.GetString("map-layer-transition-free"));
    }

    private void SweepLostAtSea(EntityUid map)
    {
        var minds = new List<EntityUid>();
        var query = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var container, out var xform))
        {
            if (xform.MapUid == map && container.Mind is { } mind && !HasComp<GhostComponent>(uid))
                minds.Add(mind);
        }

        foreach (var mind in minds)
        {
            _ghost.SpawnGhost(mind);
        }

        var players = minds.Count;

        var lost = new List<EntityUid>();
        var children = Transform(map).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            lost.Add(child);
        }

        if (lost.Count == 0)
            return;

        foreach (var uid in lost)
        {
            if (HasComp<GhostComponent>(uid))
                _xform.SetCoordinates(uid, _ticker.GetObserverSpawnPoint());
            else
                QueueDel(uid);
        }

        _adminLog.Add(LogType.EntityDelete,
            players > 0 ? LogImpact.High : LogImpact.Medium,
            $"Swept {lost.Count} entities lost at sea off {ToPrettyString(map):map}, ghosting {players} players");
    }
}
