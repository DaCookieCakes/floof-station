using System.Numerics;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Ocean;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Swimming;

public sealed partial class SwimmingSystem : VirtualController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private FloodLevelSystem _level = default!;
    [Dependency] private MapFluidSystem _fluid = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    [Dependency] private EntityQuery<BuckleComponent> _buckleQuery = default!;
    [Dependency] private EntityQuery<ClimbingComponent> _climbingQuery = default!;
    [Dependency] private EntityQuery<MagbootsComponent> _magbootsQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<StaminaComponent> _staminaQuery = default!;
    [Dependency] private EntityQuery<SwimSinkingComponent> _sinkingQuery = default!;

    private static readonly SoundSpecifier StrokeSound = new SoundCollectionSpecifier("FloodSwimStroke");

    private const float SpeedFactorStep = 0.05f;

    private float _slowStart;
    private float _slowEnd;
    private float _slowSpeed;
    private float _sweepDepth;
    private float _sweepSurge;
    private float _sweepCalm;
    private float _currentScale;
    private float _currentMaxSpeed;
    private float _currentAccel;
    private float _currentSmoothing;
    private float _swimDepth;
    private bool _lyingSwim;
    private float _lyingSwimDepth;
    private float _lyingStrokeSpeed;
    private float _strokeInterval;
    private float _strokeTime;
    private float _strokeSpeed;
    private float _strokeAccel;
    private float _glideFriction;
    private float _drownDepth;
    private float _sinkStroke;

    public float DrownDepth => _drownDepth;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<MobMoverComponent, MapInitEvent>(OnMobMapInit);
        SubscribeLocalEvent<SwimmerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SwimmerComponent, IsWeightlessEvent>(OnIsWeightless);

        Subs.CVar(_cfg, CCVars.CCVars.FloodSlowStartDepth, value => _slowStart = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSlowEndDepth, value => _slowEnd = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSlowSpeed, value => _slowSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSweepDepth, value => _sweepDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSweepSurge, value => _sweepSurge = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSweepCalm, value => _sweepCalm = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentScale, value => _currentScale = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentMaxSpeed, value => _currentMaxSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentAcceleration, value => _currentAccel = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodCurrentSmoothing, value => _currentSmoothing = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSwimDepth, value => _swimDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodLyingSwim, value => _lyingSwim = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodLyingSwimDepth, value => _lyingSwimDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodLyingStrokeSpeed, value => _lyingStrokeSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeInterval, value => _strokeInterval = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeTime, value => _strokeTime = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeSpeed, value => _strokeSpeed = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodStrokeAcceleration, value => _strokeAccel = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodGlideFriction, value => _glideFriction = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodDrownDepth, value => _drownDepth = value, true);
        Subs.CVar(_cfg, CCVars.CCVars.FloodSinkStroke, value => _sinkStroke = value, true);

        base.Initialize();
    }

    private void OnMobMapInit(Entity<MobMoverComponent> ent, ref MapInitEvent args)
    {
        if (!_level.IsIntangible(ent))
            EnsureComp<SwimmerComponent>(ent);
    }

    private void OnRefreshSpeed(Entity<SwimmerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedFactor);
    }

    private void OnIsWeightless(Entity<SwimmerComponent> ent, ref IsWeightlessEvent args)
    {
        var xform = Transform(ent);
        if (args.Handled || xform.GridUid != null || !_fluid.IsInFluid(xform))
            return;

        args.IsWeightless = false;
        args.Handled = true;
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        var query = EntityQueryEnumerator<SwimmerComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var swimmer, out var physics, out var xform))
        {
            if (prediction && !physics.Predict)
                continue;

            var volume = _level.GetVolume(xform);
            var depth = GetStandingDepth(uid, xform);
            UpdateSlowdown((uid, swimmer), depth);
            ApplyCurrent((uid, swimmer), physics, xform, volume, depth, frameTime);
        }
    }

    private void UpdateSlowdown(Entity<SwimmerComponent> ent, float depth)
    {
        var factor = 1f;
        if (depth >= _slowStart && (depth < GetSwimDepth(ent) || IsOnBottom(ent)))
        {
            var progress = Math.Clamp((depth - _slowStart) / MathF.Max(_slowEnd - _slowStart, 0.001f), 0f, 1f);
            factor = MathHelper.Lerp(1f, _slowSpeed, progress);
        }

        factor = MathF.Round(factor / SpeedFactorStep) * SpeedFactorStep;
        if (MathHelper.CloseTo(ent.Comp.SpeedFactor, factor))
            return;

        ent.Comp.SpeedFactor = factor;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void ApplyCurrent(Entity<SwimmerComponent> ent, PhysicsComponent physics, TransformComponent xform, float volume, float depth, float frameTime)
    {
        var uid = ent.Owner;
        var free = depth >= _sweepDepth &&
                   physics.BodyStatus == BodyStatus.OnGround &&
                   xform.GridUid != null &&
                   xform.GridUid != xform.MapUid &&
                   !_gravity.IsWeightless(uid) &&
                   !(_buckleQuery.TryComp(uid, out var buckle) && buckle.Buckled) &&
                   !IsOnBottom(uid);

        var measured = Vector2.Zero;
        if (free)
        {
            var gridUid = xform.GridUid!.Value;
            var grid = _gridQuery.GetComponent(gridUid);
            measured = _level.GetCurrent(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates), volume);
        }

        if (_timing.IsFirstTimePredicted)
        {
            var blend = 1f - MathF.Exp(-frameTime / MathF.Max(_currentSmoothing, 0.001f));
            ent.Comp.Current = Vector2.Lerp(ent.Comp.Current, measured, blend);
            if (measured == Vector2.Zero && ent.Comp.Current.LengthSquared() < 0.0001f)
                ent.Comp.Current = Vector2.Zero;
        }

        var current = ent.Comp.Current;
        if (!free || current == Vector2.Zero)
            return;

        var progress = Math.Clamp((depth - _sweepDepth) / MathF.Max(_swimDepth - _sweepDepth, 0.001f), 0f, 1f);
        var strength = current.Length();
        var excess = strength - MathHelper.Lerp(_sweepSurge, _sweepCalm, progress);
        if (excess <= 0f)
            return;

        var direction = _transform.GetWorldRotation(xform.GridUid!.Value).RotateVec(current / strength);
        var velocity = physics.LinearVelocity;
        SharedMoverController.Accelerate(ref velocity, direction * MathF.Min(excess * _currentScale, _currentMaxSpeed), _currentAccel, frameTime);
        PhysicsSystem.SetLinearVelocity(uid, velocity, body: physics);
    }

    public bool TrySwim(EntityUid uid, TransformComponent xform, ref Vector2 wishDir, ref float friction, ref float accel)
    {
        if (!TryComp<SwimmerComponent>(uid, out var swimmer))
            return false;

        var depth = GetStandingDepth(uid, xform);
        if (depth < GetSwimDepth(uid) || IsOnBottom(uid))
            return false;

        friction = _glideFriction;
        var direction = wishDir;
        wishDir = Vector2.Zero;

        if (direction == Vector2.Zero)
            return true;

        var curTime = _timing.CurTime;
        var sinceStroke = (curTime - swimmer.LastStroke).TotalSeconds;
        if (sinceStroke >= _strokeInterval)
        {
            swimmer.LastStroke = curTime;
            sinceStroke = 0;
            Dirty(uid, swimmer);
            _audio.PlayPredicted(StrokeSound, uid, uid);
        }

        if (sinceStroke < _strokeTime)
        {
            var speed = depth < _swimDepth ? MathF.Min(_strokeSpeed, _lyingStrokeSpeed) : _strokeSpeed;
            wishDir = direction.Normalized() * speed * (IsSinking(uid) ? _sinkStroke : 1f);
            accel = _strokeAccel;
        }

        return true;
    }

    public float GetSwimDepth(EntityUid uid)
    {
        return CanSwimLying(uid) ? MathF.Min(_lyingSwimDepth, _swimDepth) : _swimDepth;
    }

    public bool IsSwimmingLying(EntityUid uid, TransformComponent xform)
    {
        return CanSwimLying(uid) && !IsOnBottom(uid) && GetStandingDepth(uid, xform) >= MathF.Min(_lyingSwimDepth, _swimDepth);
    }

    private bool CanSwimLying(EntityUid uid)
    {
        return _lyingSwim && _standing.IsDown(uid) && _blocker.CanMove(uid);
    }

    public bool IsOnBottom(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "shoes", out var boots) &&
               _magbootsQuery.HasComp(boots) &&
               _toggle.IsActivated(boots.Value);
    }

    public bool IsSinking(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "outerClothing", out var suit) && _sinkingQuery.HasComp(suit);
    }

    public bool IsSubmerged(EntityUid uid, TransformComponent xform)
    {
        var depth = GetStandingDepth(uid, xform);
        if (depth >= _drownDepth)
            return true;

        return depth >= _swimDepth &&
               (IsOnBottom(uid) ||
                IsSinking(uid) ||
                _mobState.IsIncapacitated(uid) ||
                _staminaQuery.TryComp(uid, out var stamina) && stamina.Critical);
    }

    public float GetStandingDepth(EntityUid uid, TransformComponent xform)
    {
        var depth = _level.GetDepth(xform);
        if (depth <= 0f || depth >= _drownDepth ||
            !_climbingQuery.TryComp(uid, out var climbing) || !climbing.IsClimbing ||
            xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return depth;

        return MathF.Max(depth - _level.GetSurface(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates)), 0f);
    }
}
