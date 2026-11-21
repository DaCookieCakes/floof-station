using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared._Floof.MapLayer;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Floof.MapLayer;

public sealed partial class MapLayerSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    private readonly List<(EntityUid Layer, EntityUid Destination)> _arrived = new();
    private readonly List<EntityUid> _grids = new();
    private readonly List<EntityUid> _phantoms = new();

    private const int MaxPushRadius = 256;

    private void UpdateArrivals()
    {
        var now = _timing.CurTime;
        _arrived.Clear();
        var query = EntityQueryEnumerator<MapLayerPoolComponent, MapLayerTransitionComponent>();
        while (query.MoveNext(out var uid, out var pooled, out var transition))
        {
            if (pooled.InUse &&
                now >= transition.StartTime + transition.Duration &&
                GetDestination(transition) is { } destination)
                _arrived.Add((uid, destination));
        }

        foreach (var (layer, destination) in _arrived)
        {
            ArriveOn(layer, destination);
        }
    }

    private EntityUid? GetDestination(MapLayerTransitionComponent transition)
    {
        return HasComp<MapLayerComponent>(transition.To) ? transition.To : null;
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

        TransferContents(map);
        RemoveLayer(map);
        pooled.InUse = false;
        _metaData.SetEntityName(map, Loc.GetString("map-layer-transition-free"));
    }

    private void TransferContents(EntityUid map)
    {
        if (!TryComp<MapLayerTransitionComponent>(map, out var transition) ||
            GetDestination(transition) is not { } destination)
        {
            if (Transform(map).ChildCount is > 0 and var count)
                Log.Error($"Couldn't transfer {count} entities from {ToPrettyString(map)}: no destination layer");
            return;
        }

        var down = transition.EndZ < transition.StartZ;
        var mapXform = Transform(map);
        if (mapXform.ChildCount == 0)
            return;

        var contents = new List<EntityUid>(mapXform.ChildCount);
        var children = mapXform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            contents.Add(child);
        }

        var players = 0;
        foreach (var uid in contents)
        {
            var xform = Transform(uid);
            var (position, rotation) = _xform.GetWorldPositionRotation(xform);
            var bounds = TryComp<MapGridComponent>(uid, out var grid)
                ? _xform.GetWorldMatrix(xform).TransformBox(grid.LocalAABB)
                : Box2.CenteredAround(position, new Vector2(0.01f));
            var offset = FindClearOffset(destination, bounds);
            _xform.SetCoordinates(uid, xform, new EntityCoordinates(destination, position + offset), rotation);

            if (grid != null || HasComp<GhostComponent>(uid) || !TryComp<ActorComponent>(uid, out var actor))
                continue;

            players++;
            var direction = down ? "down" : "up";
            _chat.DispatchServerMessage(actor.PlayerSession,
                Loc.GetString(offset != Vector2.Zero ? "map-layer-transfer-blocked" : "map-layer-transfer-landed", ("direction", direction)));
        }

        _adminLog.Add(LogType.Teleport,
            players > 0 ? LogImpact.High : LogImpact.Medium,
            $"Transferred {contents.Count} entities from {ToPrettyString(map):map} to {ToPrettyString(destination):map}, {players} players");
    }

    private Vector2 FindClearOffset(EntityUid map, Box2 bounds)
    {
        if (IsClear(map, bounds))
            return Vector2.Zero;

        for (var radius = 1; radius <= MaxPushRadius; radius++)
        {
            var steps = (int) MathF.Ceiling(MathF.Tau * radius);
            for (var i = 0; i < steps; i++)
            {
                var offset = new Angle(MathF.Tau * i / steps).ToVec() * radius;
                if (IsClear(map, bounds.Translated(offset)))
                    return offset;
            }
        }

        return Vector2.Zero;
    }

    private bool IsClear(EntityUid map, Box2 bounds)
    {
        var clear = true;
        _map.FindGridsIntersecting(map,
            bounds,
            ref clear,
            static (EntityUid _, MapGridComponent _, ref bool clear) =>
            {
                clear = false;
                return false;
            },
            includeMap: false);
        return clear;
    }
}
