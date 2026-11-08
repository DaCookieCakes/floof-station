/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Adapted from Crystal Edge's z-level eyes (CEZLevelsSystem.View.cs).

using System.Numerics;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.MapUnderlay;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Floof.MapUnderlay;

/// <summary>
///     Sends the map below to whoever is on a map with <see cref="MapUnderlayComponent"/> and has it turned on, through
///     an eye down there that follows them. Only while it's close enough to show, so nobody gets it for nothing.
/// </summary>
public sealed partial class MapUnderlaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetConfigurationManager _netCfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;

    private static readonly EntProtoId EyeProto = "MapUnderlayEye";
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    // Sent a little before it shows, and kept a little past, so it doesn't pop in or flicker at the edge.
    private const float SendRange = 1.3f;
    private const float KeepRange = 1.5f;

    private readonly Dictionary<EntityUid, HashSet<ICommonSession>> _viewers = new();
    private readonly HashSet<ICommonSession> _wanted = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapUnderlayComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<MapUnderlayComponent> ent, ref ComponentShutdown args)
    {
        RemoveEye(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<MapUnderlayComponent>();
        while (query.MoveNext(out var uid, out var underlay))
        {
            UpdateUnderlay((uid, underlay));
        }
    }

    private void UpdateUnderlay(Entity<MapUnderlayComponent> ent)
    {
        if (!Exists(ent.Comp.Map))
        {
            RemoveEye(ent);
            return;
        }

        var ev = new MapUnderlayShowEvent(ent.Comp.Eye == null ? SendRange : KeepRange);
        RaiseLocalEvent(ent, ref ev);

        _wanted.Clear();
        var center = Vector2.Zero;
        if (ev.Share > 0f)
        {
            foreach (var session in _player.Sessions)
            {
                if (session.AttachedEntity is not { } player ||
                    Transform(player).MapUid != ent.Owner ||
                    !_netCfg.GetClientCVar(session.Channel, CCVars.MapUnderlay))
                    continue;

                _wanted.Add(session);
                center += _xform.GetWorldPosition(player);
            }
        }

        if (_wanted.Count == 0)
        {
            RemoveEye(ent);
            return;
        }

        center /= _wanted.Count;
        var below = new EntityCoordinates(ent.Comp.Map.Value, ent.Comp.ToBelow(center));
        if (ent.Comp.Eye is { } eye && Exists(eye))
        {
            _xform.SetCoordinates(eye, below);
        }
        else
        {
            eye = SpawnAtPosition(EyeProto, below);
            Transform(eye).GridTraversal = false;
            ent.Comp.Eye = eye;
        }

        var viewers = _viewers.GetOrNew(ent.Owner);
        foreach (var session in viewers)
        {
            if (!_wanted.Contains(session))
                _viewSubscriber.RemoveViewSubscriber(eye, session);
        }

        foreach (var session in _wanted)
        {
            if (!viewers.Contains(session))
                _viewSubscriber.AddViewSubscriber(eye, session);
        }

        viewers.Clear();
        viewers.UnionWith(_wanted);
    }

    private void RemoveEye(Entity<MapUnderlayComponent> ent)
    {
        _viewers.Remove(ent.Owner);
        if (ent.Comp.Eye is { } eye)
            QueueDel(eye);

        ent.Comp.Eye = null;
    }
}
