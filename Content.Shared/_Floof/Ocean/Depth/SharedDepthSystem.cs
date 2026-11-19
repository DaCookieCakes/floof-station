using System.Numerics;
using Content.Shared._Floof.MapLayer;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._Floof.Ocean.Depth;

public abstract partial class SharedDepthSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedMapLayerSystem _layers = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<DepthComponent> _depthQuery;
    [Dependency] private EntityQuery<DepthTransitionComponent> _transitionQuery;

    public float? GetDepth(EntityUid map, Vector2 position)
    {
        if (_transitionQuery.TryComp(map, out var transition))
        {
            var t = _layers.TryGetTransitionProgress(map, out var progress) ? progress : 1f;
            return transition.StartDepth + (transition.EndDepth - transition.StartDepth) * t;
        }

        if (!_depthQuery.TryComp(map, out var depth))
            return null;

        return depth.Depth;
    }

    public float? GetDepth(MapCoordinates coords)
    {
        if (!_map.TryGetMap(coords.MapId, out var map))
            return null;

        return GetDepth(map.Value, coords.Position);
    }

    public float? GetDepth(EntityUid uid)
    {
        return GetDepth(_transform.GetMapCoordinates(uid));
    }

    public void SetDepth(EntityUid map, float depth)
    {
        var comp = EnsureComp<DepthComponent>(map);
        comp.Depth = depth;
        Dirty(map, comp);
    }

    protected void SetSeed(Entity<DepthVariationComponent> ent, int seed)
    {
        ent.Comp.Seed = seed;
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnTransitionStarted(ref MapLayerTransitionStartedEvent ev)
    {
        if (GetDepth(ev.From, ev.Position) is not { } start || GetDepth(ev.To, ev.Position) is not { } end)
        {
            RemComp<DepthTransitionComponent>(ev.Layer);
            return;
        }

        var transition = EnsureComp<DepthTransitionComponent>(ev.Layer);
        transition.StartDepth = start;
        transition.EndDepth = end;
        Dirty(ev.Layer, transition);
    }

    [SubscribeLocalEvent]
    private void OnLayerTransitionRemoved(Entity<MapLayerTransitionComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsServer && !TerminatingOrDeleted(ent))
            RemComp<DepthTransitionComponent>(ent);
    }
}
