using System.Numerics;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.MapLayer.Transition;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Floof.MapLayer.Transition;

public sealed partial class SonarBlipSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private static readonly Vector2i[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    public const float RingAlpha = 0.3f;

    private const float PassiveBrightness = 0.3f;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(EntityUid Map, float Gap)> _gaps = new();
    private readonly List<SonarTarget> _found = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SonarViewComponent, SonarComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var view, out var sonar, out var xform))
        {
            if (xform.GridUid is not { } subUid ||
                !TryComp(subUid, out MapGridComponent? grid) ||
                !TryComp(subUid, out TransformComponent? subXform) ||
                subXform.MapUid == null)
            {
                Reset(view, null);
                continue;
            }

            if (view.Sub != subUid)
                Reset(view, subUid);

            Entity<MapGridComponent, TransformComponent> sub = (subUid, grid, subXform);
            view.Blips.RemoveAll(b => now - b.Time > b.Lifetime);

            if (now >= view.NextPassive && TryComp(uid, out PassiveSonarComponent? passive))
            {
                view.NextPassive = now + passive.Interval;
                Passive(view, passive, sub, now);
            }

            if (sonar.Active && view.Ping == null && now >= view.NextPing)
                StartPing(view, sonar, sub, now);

            if (view.Ping != null)
                UpdatePing(view, sonar, now);
        }
    }

    public bool TryGetCenter(EntityUid sub, out Vector2 center, out EntityUid map)
    {
        center = default;
        map = default;
        if (!TryComp(sub, out MapGridComponent? grid) ||
            !TryComp(sub, out TransformComponent? xform) ||
            xform.MapUid is not { } ownMap)
            return false;

        center = Vector2.Transform(grid.LocalAABB.Center, _xform.GetWorldMatrix(xform));
        map = ownMap;
        return true;
    }

    public float GetParallax(float? z, EntityUid own)
    {
        if (z is not { } blipZ || !_layers.TryGetZ(own, out var ownZ))
            return 1f;

        return 1f / MapLayerSystem.GetZoom(ownZ - blipZ);
    }

    private float? GetZ(EntityUid map)
    {
        return _layers.TryGetZ(map, out var z) ? z : null;
    }

    public float GetPingRadius(SonarPing ping, SonarComponent sonar, TimeSpan now)
    {
        var sweep = MathF.Max((float) sonar.Interval.TotalSeconds / 2f, 0.01f);
        return MathF.Min((float) (now - ping.Start).TotalSeconds / sweep, 1f) * sonar.Range;
    }

    public static float GetBrightness(float distance, float range)
    {
        var left = Math.Clamp(1f - distance / range, 0f, 1f);
        return left * left;
    }

    private void Reset(SonarViewComponent view, EntityUid? sub)
    {
        view.Sub = sub;
        view.Blips.Clear();
        view.Ping = null;
        view.NextPassive = TimeSpan.Zero;
        view.NextPing = TimeSpan.Zero;
    }

    private void Passive(SonarViewComponent view,
        PassiveSonarComponent passive,
        Entity<MapGridComponent, TransformComponent> sub,
        TimeSpan now)
    {
        Find(sub, passive.Range, _found);
        foreach (var target in _found)
        {
            var strength = PassiveBrightness * GetBrightness(target.Distance, passive.Range);
            view.Blips.Add(new SonarBlip(target.Position, GetZ(target.Map), now, passive.FadeTime ?? passive.Interval, strength));
        }
    }

    private void StartPing(SonarViewComponent view,
        SonarComponent sonar,
        Entity<MapGridComponent, TransformComponent> sub,
        TimeSpan now)
    {
        var hull = sub.Comp1.LocalAABB;
        var ping = new SonarPing
        {
            Start = now,
            Center = Vector2.Transform(hull.Center, _xform.GetWorldMatrix(sub.Comp2)),
            HullRadius = MathF.Max(hull.Width, hull.Height) / 2f,
        };

        Find(sub, sonar.Range, ping.Targets);
        ping.Targets.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        view.Ping = ping;
        view.NextPing = now + sonar.Interval;
    }

    private void UpdatePing(SonarViewComponent view, SonarComponent sonar, TimeSpan now)
    {
        var ping = view.Ping!;
        var swept = GetPingRadius(ping, sonar, now);
        for (; ping.Next < ping.Targets.Count && ping.Targets[ping.Next].Distance <= swept; ping.Next++)
        {
            var target = ping.Targets[ping.Next];
            if (!TryComp(target.Grid, out MapGridComponent? grid) ||
                Transform(target.Grid).MapUid is not { } map)
                continue;

            var position = _map.GridTileToWorldPos(target.Grid, grid, target.Tile);
            var strength = GetBrightness(target.Distance, sonar.Range);
            view.Blips.Add(new SonarBlip(position, GetZ(map), now, sonar.FadeTime ?? sonar.Interval, strength));
        }

        if (swept >= sonar.Range)
            view.Ping = null;
    }

    private void Find(Entity<MapGridComponent, TransformComponent> sub, float range, List<SonarTarget> found)
    {
        found.Clear();
        var hull = sub.Comp1.LocalAABB;
        var (_, _, subToWorld, worldToSub) = _xform.GetWorldPositionRotationMatrixWithInv(sub.Comp2);
        var hullWorld = subToWorld.TransformBox(hull);
        var center = hullWorld.Center;
        var reach = hullWorld.Enlarged(range);
        var own = sub.Comp2.MapUid!.Value;

        if (!_layers.TryGetGaps(own, _gaps))
            _gaps.Add((own, 0f));

        foreach (var (map, gap) in _gaps)
        {
            if (MathF.Abs(gap) >= range || !TryComp(map, out MapComponent? mapComp))
                continue;

            var parallax = 1f / MapLayerSystem.GetZoom(gap);
            var area = new Box2(
                center + (reach.BottomLeft - center) / parallax,
                center + (reach.TopRight - center) / parallax);

            _grids.Clear();
            _map.FindGridsIntersecting(mapComp.MapId, area, ref _grids, includeMap: false);
            foreach (var other in _grids)
            {
                if (other.Owner == sub.Owner || HasComp<PhantomGridComponent>(other))
                    continue;

                foreach (var tile in _map.GetTilesIntersecting(other, other.Comp, area))
                {
                    if (!IsOutline(other, tile.GridIndices))
                        continue;

                    var world = _map.GridTileToWorldPos(other, other.Comp, tile.GridIndices);
                    var drawn = center + (world - center) * parallax;
                    var distance = DistanceToBox(Vector2.Transform(drawn, worldToSub), hull);
                    if (distance <= range)
                        found.Add(new SonarTarget(other, tile.GridIndices, world, map, distance));
                }
            }
        }
    }

    private bool IsOutline(Entity<MapGridComponent> grid, Vector2i indices)
    {
        foreach (var offset in Neighbors)
        {
            if (!_map.TryGetTileRef(grid, grid.Comp, indices + offset, out var neighbor) || neighbor.Tile.IsEmpty)
                return true;
        }

        return false;
    }

    private static float DistanceToBox(Vector2 point, Box2 box)
    {
        var outside = Vector2.Max(Vector2.Abs(point - box.Center) - box.Size / 2f, Vector2.Zero);
        return outside.Length();
    }
}
