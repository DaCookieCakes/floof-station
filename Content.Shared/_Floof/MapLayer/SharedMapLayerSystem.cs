using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.MapLayer;

public abstract partial class SharedMapLayerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<(EntityUid Map, float Z)> _bracketLayers = new();

    [PublicAPI]
    public void SetLayer(EntityUid map, MapLayerStack stack, float z, bool noOp = false)
    {
        var layer = EnsureComp<MapLayerComponent>(map);
        layer.Stack = stack;
        layer.Z = z;
        layer.NoOp = noOp;
        Dirty(map, layer);
    }

    [PublicAPI]
    public bool StartTransition(EntityUid layer,
        EntityUid from,
        EntityUid to,
        Vector2 position,
        TimeSpan duration,
        MapLayerEasing easing = MapLayerEasing.InOutSine)
    {
        if (!TryComp<MapLayerComponent>(from, out var fromLayer) ||
            !TryComp<MapLayerComponent>(to, out var toLayer) ||
            fromLayer.Stack != toLayer.Stack ||
            toLayer.NoOp ||
            !TryGetZ((from, fromLayer), out var startZ) ||
            !TryGetZ((to, toLayer), out var endZ))
            return false;

        SetLayer(layer, fromLayer.Stack, endZ);

        var transition = EnsureComp<MapLayerTransitionComponent>(layer);
        transition.StartZ = startZ;
        transition.EndZ = endZ;
        transition.StartTime = _timing.CurTime;
        transition.Duration = duration;
        transition.Easing = easing;
        Dirty(layer, transition);

        var ev = new MapLayerTransitionStartedEvent(layer, from, to, position);
        RaiseLocalEvent(layer, ref ev, true);
        return true;
    }

    [PublicAPI]
    public void RemoveLayer(EntityUid map)
    {
        RemComp<MapLayerTransitionComponent>(map);
        RemComp<MapLayerComponent>(map);
    }

    [PublicAPI]
    public bool TryGetZ(Entity<MapLayerComponent?> layer, out float z)
    {
        z = 0f;
        if (!Resolve(layer, ref layer.Comp, false))
            return false;

        z = TryComp<MapLayerTransitionComponent>(layer, out var transition)
            ? GetTransitionZ(transition)
            : layer.Comp.Z;
        return true;
    }

    [PublicAPI]
    public bool TryGetTransitionProgress(EntityUid map, out float progress)
    {
        progress = 0f;
        if (!TryComp<MapLayerTransitionComponent>(map, out var transition))
            return false;

        progress = GetEasedProgress(transition);
        return true;
    }

    [PublicAPI]
    public bool TryGetStack(EntityUid uid, out MapLayerStack stack)
    {
        stack = default;
        if (Transform(uid).MapUid is not { } map || !TryComp<MapLayerComponent>(map, out var layer))
            return false;

        stack = layer.Stack;
        return true;
    }

    [PublicAPI]
    public bool TryGetVisibility(EntityUid uid, out float belowRange, out float aboveFade, bool roofed = false)
    {
        belowRange = 0f;
        aboveFade = 0f;
        if (Transform(uid).MapUid is not { } map || !TryComp<MapLayerComponent>(map, out var layer))
            return false;

        belowRange = layer.BelowRange;
        aboveFade = roofed ? layer.RoofedAboveFade : layer.AboveFade;
        return true;
    }

    [PublicAPI]
    public bool TryGetRoof(EntityUid map, out MapLayerRoof roof)
    {
        roof = default;
        if (!TryComp<MapLayerComponent>(map, out var layer))
            return false;

        roof = new MapLayerRoof(layer.RoofFadeStart, layer.RoofFadeEnd, layer.RoofTexture, layer.RoofColor);
        return true;
    }

    [PublicAPI]
    public void GetLayers(MapLayerStack stack, List<(EntityUid Map, float Z)> layers, bool noOp = false)
    {
        layers.Clear();
        var query = EntityQueryEnumerator<MapLayerComponent>();
        while (query.MoveNext(out var uid, out var layer))
        {
            if (layer.Stack == stack && (noOp || !layer.NoOp) && TryGetZ((uid, layer), out var z))
                layers.Add((uid, z));
        }

        layers.Sort((a, b) => a.Z.CompareTo(b.Z));
    }

    [PublicAPI]
    public bool TryGetBracket<T>(MapLayerStack stack,
        float z,
        out Entity<T> above,
        out Entity<T> below,
        out float t) where T : IComponent
    {
        above = default;
        below = default;
        t = 0f;

        GetLayers(stack, _bracketLayers, true);
        var aboveZ = 0f;
        var belowZ = 0f;
        var hasAbove = false;
        var hasBelow = false;
        foreach (var (map, layerZ) in _bracketLayers)
        {
            if (HasComp<MapLayerTransitionComponent>(map) || !TryComp<T>(map, out var comp))
                continue;

            if (layerZ <= z)
            {
                below = (map, comp);
                belowZ = layerZ;
                hasBelow = true;
                continue;
            }

            above = (map, comp);
            aboveZ = layerZ;
            hasAbove = true;
            break;
        }

        if (!hasAbove && !hasBelow)
            return false;

        if (!hasAbove)
        {
            above = below;
            return true;
        }

        if (!hasBelow)
        {
            below = above;
            return true;
        }

        t = aboveZ > belowZ ? Math.Clamp((z - belowZ) / (aboveZ - belowZ), 0f, 1f) : 0f;
        return true;
    }

    [PublicAPI]
    public bool TryGetBelowTop(EntityUid map, out float belowTop)
    {
        belowTop = 0f;
        if (!TryComp<MapLayerComponent>(map, out var layer) || !TryGetZ((map, layer), out var z))
            return false;

        var top = z;
        var query = EntityQueryEnumerator<MapLayerComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (other.Stack == layer.Stack && !other.NoOp && TryGetZ((uid, other), out var otherZ))
                top = MathF.Max(top, otherZ);
        }

        belowTop = top - z;
        return true;
    }

    [PublicAPI]
    public bool TryGetGaps(EntityUid map, List<(EntityUid Map, float Gap)> gaps)
    {
        gaps.Clear();
        if (!TryGetStack(map, out var stack) || !TryGetZ(map, out var z))
            return false;

        GetLayers(stack, gaps);
        for (var i = 0; i < gaps.Count; i++)
        {
            gaps[i] = (gaps[i].Map, z - gaps[i].Gap);
        }

        return true;
    }

    private float GetTransitionZ(MapLayerTransitionComponent transition)
    {
        var eased = GetEasedProgress(transition);
        return transition.StartZ + (transition.EndZ - transition.StartZ) * eased;
    }

    private float GetEasedProgress(MapLayerTransitionComponent transition)
    {
        var progress = transition.Duration > TimeSpan.Zero
            ? Math.Clamp((float) ((_timing.CurTime - transition.StartTime) / transition.Duration), 0f, 1f)
            : 1f;

        return transition.Easing switch
        {
            MapLayerEasing.InOutSine => Easings.InOutSine(progress),
            MapLayerEasing.InOutCubic => Easings.InOutCubic(progress),
            MapLayerEasing.InSine => Easings.InSine(progress),
            MapLayerEasing.OutSine => Easings.OutSine(progress),
            _ => progress,
        };
    }
}

public readonly record struct MapLayerRoof(float FadeStart, float FadeEnd, ResPath Texture, Color Color)
{
    public float GetAlpha(float gap)
    {
        if (FadeEnd <= FadeStart)
            return gap >= FadeEnd ? 1f : 0f;

        return Math.Clamp((gap - FadeStart) / (FadeEnd - FadeStart), 0f, 1f);
    }
}
