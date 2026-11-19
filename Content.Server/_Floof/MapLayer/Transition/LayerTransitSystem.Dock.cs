using System.Numerics;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.MapLayer.Transition;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Floof.MapLayer.Transition;

public sealed partial class LayerTransitSystem
{
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromSeconds(1);

    private readonly List<(float Distance, Entity<DockingComponent> Dock)> _dockTargets = new();
    private TimeSpan _nextPreview;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateApproaches();

        if (_timing.CurTime < _nextPreview)
            return;

        _nextPreview = _timing.CurTime + PreviewInterval;
        UpdatePreviews();
    }

    [SubscribeLocalEvent]
    private void OnDock(EntityUid uid, ShuttleConsoleComponent comp, SubDockMessage args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } sub ||
            xform.MapUid is not { } map ||
            !TryGetSeabedBelow(map, out var z, out var seabed))
            return;

        switch (FindDock(sub, seabed, out var config))
        {
            case SubDockSearch.NoneInRange:
                _popup.PopupEntity(Loc.GetString("sub-helm-dock-none"), uid, args.Actor);
                return;
            case SubDockSearch.NoFit:
                _popup.PopupEntity(Loc.GetString("sub-helm-dock-no-fit"), uid, args.Actor);
                return;
        }

        var (endPosition, endRotation) = GetDockPose(config);
        var center = Comp<MapGridComponent>(sub).LocalAABB.Center;

        var body = Comp<PhysicsComponent>(sub);
        var velocity = body.LinearVelocity;
        var angularVelocity = body.AngularVelocity;

        if (StartTransition(sub, map, seabed) is null)
            return;

        _shuttle.Disable(sub);

        var (startPosition, startRotation) = _xform.GetWorldPositionRotation(sub);
        var approach = EnsureComp<DockApproachComponent>(sub);
        approach.StartPosition = startPosition + startRotation.RotateVec(center);
        approach.StartRotation = startRotation;
        approach.StartVelocity = velocity;
        approach.StartAngularVelocity = angularVelocity;
        approach.EndPosition = endPosition + endRotation.RotateVec(center);
        approach.EndRotation = endRotation;
        approach.StartTime = _timing.CurTime;
        approach.Duration = GetDuration(z, 0f);
        approach.Docks.Clear();
        foreach (var (subDock, seabedDock, _, _) in config.Docks)
        {
            approach.Docks.Add((subDock, seabedDock));
        }
    }

    private void UpdateApproaches()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DockApproachComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var approach, out var grid))
        {
            var progress = approach.Duration > TimeSpan.Zero
                ? Math.Clamp((float) ((now - approach.StartTime) / approach.Duration), 0f, 1f)
                : 1f;
            var seconds = (float) approach.Duration.TotalSeconds;

            var s = progress;
            var start = s * s * s - 2f * s * s + s;
            var end = -2f * s * s * s + 3f * s * s;

            var position = approach.StartPosition +
                           approach.StartVelocity * (seconds * start) +
                           (approach.EndPosition - approach.StartPosition) * end;
            var turn = Angle.ShortestDistance(approach.StartRotation, approach.EndRotation).Theta;
            var rotation = approach.StartRotation +
                           new Angle(approach.StartAngularVelocity * seconds * start + turn * end);
            SetCenterPose((uid, grid), position, rotation);
        }
    }

    private void SnapToDock(Entity<DockApproachComponent> sub)
    {
        SetCenterPose((sub, Comp<MapGridComponent>(sub)), sub.Comp.EndPosition, sub.Comp.EndRotation);
    }

    private void ConnectDocks(Entity<DockApproachComponent> sub)
    {
        foreach (var (subDock, seabedDock) in sub.Comp.Docks)
        {
            if (TryComp<DockingComponent>(subDock, out var a) && !a.Docked &&
                TryComp<DockingComponent>(seabedDock, out var b) && !b.Docked)
                _dock.Dock((subDock, a), (seabedDock, b));
        }

        RemComp(sub, sub.Comp);
        RemComp<SubDockPreviewComponent>(sub);
    }

    private void UpdatePreviews()
    {
        var query = EntityQueryEnumerator<ShuttleComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (HasComp<DockApproachComponent>(uid))
                continue;

            if (xform.MapUid is not { } map ||
                !TryGetSeabedBelow(map, out _, out var seabed) ||
                FindDock(uid, seabed, out var config) != SubDockSearch.Found)
            {
                RemComp<SubDockPreviewComponent>(uid);
                continue;
            }

            var (position, rotation) = GetDockPose(config);
            var preview = EnsureComp<SubDockPreviewComponent>(uid);
            if (preview.Position.EqualsApprox(position) && preview.Rotation.EqualsApprox(rotation))
                continue;

            preview.Position = position;
            preview.Rotation = rotation;
            Dirty(uid, preview);
        }
    }

    private bool TryGetSeabedBelow(EntityUid map, out float z, out EntityUid seabed)
    {
        seabed = EntityUid.Invalid;
        z = 0f;
        if (!HasComp<MapLayerCruiseComponent>(map) ||
            HasComp<MapLayerPoolComponent>(map) ||
            !_layers.TryGetStack(map, out var stack) ||
            !_layers.TryGetZ(map, out z))
            return false;

        var found = false;
        var seabedZ = 0f;
        _layers.GetLayers(stack, _stackLayers);
        foreach (var (layer, layerZ) in _stackLayers)
        {
            if (layerZ >= z || !HasComp<MapLayerSeabedComponent>(layer))
                continue;

            if (found && layerZ <= seabedZ)
                continue;

            seabed = layer;
            seabedZ = layerZ;
            found = true;
        }

        return found;
    }

    private (Vector2 Position, Angle Rotation) GetDockPose(DockingConfig config)
    {
        var position = _xform.ToMapCoordinates(config.Coordinates).Position;
        var rotation = config.Angle + _xform.GetWorldRotation(config.Coordinates.EntityId);
        return (position, rotation);
    }

    private void SetCenterPose(Entity<MapGridComponent> sub, Vector2 center, Angle rotation)
    {
        var origin = center - rotation.RotateVec(sub.Comp.LocalAABB.Center);
        _xform.SetWorldPositionRotation(sub, origin, rotation);
    }

    private SubDockSearch FindDock(EntityUid sub, EntityUid seabed, out DockingConfig config)
    {
        config = default!;
        var range = _cfg.GetCVar(CCVars.SubDockRange);
        var subDocks = _dock.GetDocks(sub);

        _dockTargets.Clear();
        var query = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var dock, out var xform))
        {
            if (xform.MapUid != seabed || xform.GridUid == null || dock.Docked)
                continue;

            var position = _xform.GetWorldPosition(xform);
            var closest = float.MaxValue;
            foreach (var subDock in subDocks)
            {
                if (!subDock.Comp.Docked)
                    closest = MathF.Min(closest, (_xform.GetWorldPosition(subDock) - position).Length());
            }

            if (closest <= range)
                _dockTargets.Add((closest, (uid, dock)));
        }

        _dockTargets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var heading = _xform.GetWorldRotation(sub);
        foreach (var (_, target) in _dockTargets)
        {
            var grid = Transform(target).GridUid!.Value;
            var gridRotation = _xform.GetWorldRotation(grid);
            DockingConfig? best = null;
            var bestTurn = double.MaxValue;

            foreach (var candidate in _dock.GetDockingConfigs(sub, grid, subDocks, _dock.GetDocks(grid)))
            {
                if (!candidate.Docks.Exists(d => d.DockBUid == target.Owner))
                    continue;

                var turn = Math.Abs(Angle.ShortestDistance(heading, candidate.Angle + gridRotation).Theta);
                if (turn < bestTurn)
                {
                    best = candidate;
                    bestTurn = turn;
                }
            }

            if (best == null)
                continue;

            best.TargetGrid = grid;
            config = best;
            return SubDockSearch.Found;
        }

        return _dockTargets.Count > 0 ? SubDockSearch.NoFit : SubDockSearch.NoneInRange;
    }
}

public enum SubDockSearch : byte
{
    NoneInRange,

    NoFit,

    Found,
}
