using System.Linq;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Misc;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.Link;

/// <summary>
/// This handles teleporting entities from a portal to a linked portal.
/// Uses <see cref="LinkedEntitySystem"/> to get linked portals.
/// </summary>
/// <seealso cref="StaticPortalComponent"/>
public abstract partial class SharedStaticPortalSystem : EntitySystem
{
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;

    private const string PortalFixture = "portalFixture";
    private const string TimeoutFixture = "portalTimeout";
    private const string ProjectileFixture = "projectile";
    private static readonly ProtoId<TagPrototype> ShowTraverseVerbTag = "AllowPortalTraversal";
    private static readonly ProtoId<TagPrototype> PreventCollisionTag = "PreventPortalCollision";
    
    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<StaticPortalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        // Traversal altverb for ghosts to use that bypasses normal functionality
        if (!args.CanAccess || !_tag.HasTag(args.User, ShowTraverseVerbTag))
            return;

        // Don't use the verb with unlinked portals
        // (this is only intended to be useful for ghosts to see where a linked portal leads)
        var disabled = !TryComp<LinkedEntityComponent>(ent, out var link) || link.LinkedEntities.Count == 0;

        var subject = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Priority = 11,
            Act = () =>
            {
                if (link == null || disabled)
                    return;

                // check prediction
                if (_netMan.IsClient && !CanPredictTeleport((ent, link)))
                    return;

                var destination = link.LinkedEntities.First();

                TeleportEntity(ent, subject, Transform(destination).Coordinates, destination, false);
            },
            Disabled = disabled,
            Text = Loc.GetString("portal-component-ghost-traverse"),
            Message = disabled
                ? Loc.GetString("portal-component-no-linked-entities")
                : Loc.GetString("portal-component-can-ghost-traverse"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"))
        });
    }
    
    [SubscribeLocalEvent]
    private void OnCollide(Entity<StaticPortalComponent> ent, ref StartCollideEvent args)
    {
        if (!ShouldCollide(args.OurFixtureId, args.OtherFixtureId, args.OurFixture, args.OtherFixture))
            return;

        var subject = args.OtherEntity;

        if (_tag.HasTag(args.OtherEntity, PreventCollisionTag))
            return;

        // best not.
        if (Transform(subject).Anchored)
            return;

        // if they came from another portal, just return and wait for them to exit the portal
        // checked before breaking pulls, so pulled entities work.
        if (HasComp<StaticPortalTimeoutComponent>(subject))
        {
            return;
        }

        // a closed portal is hidden, so no popup
        if (ent.Comp.Open < ent.Comp.Threshold)
        {
            if (ent.Comp.Open > 0f)
                _popup.PopupEntity(Loc.GetString("static-portal-too-small"), ent, subject);
            return;
        }

        // a pulled subject takes its puller along: the puller goes through in its place
        if (TryComp<PullableComponent>(subject, out var pullable)
            && pullable.Puller is { } puller
            && CanTakePuller(puller))
        {
            subject = puller;
        }

        // break pulls before portal enter so we don't break shit
        if (TryComp(subject, out pullable) && pullable.BeingPulled)
        {
            _pulling.TryStopPull(subject, pullable);
        }

        // whatever the subject pulls comes along, anything that one pulls stays behind
        EntityUid? pulled = null;
        DistanceJoint? oldJoint = null;
        if (TryComp<PullerComponent>(subject, out var pullerComp)
            && TryComp<PullableComponent>(pullerComp.Pulling, out var subjectPulling))
        {
            pulled = pullerComp.Pulling.Value;
            oldJoint = GetPullJoint(pulled.Value, subjectPulling);
            _pulling.TryStopPull(pulled.Value, subjectPulling);

            if (TryComp<PullerComponent>(pulled, out var chainPuller)
                && TryComp<PullableComponent>(chainPuller.Pulling, out var chainPulling))
            {
                _pulling.TryStopPull(chainPuller.Pulling.Value, chainPulling);
            }

            if (_tag.HasTag(pulled.Value, PreventCollisionTag))
                pulled = null;
            else
                _joints.RemoveJoint(pulled.Value, SharedGrapplingGunSystem.GrapplingJoint);
        }

        // also break grapple joints
        _joints.RemoveJoint(subject, SharedGrapplingGunSystem.GrapplingJoint);

        if (TryComp<LinkedEntityComponent>(ent, out var link) && link.LinkedEntities.Count != 0)
        {
            // check prediction
            if (_netMan.IsClient && !CanPredictTeleport((ent, link)))
                return;

            // LinkKey pairs exactly one partner
            var target = link.LinkedEntities.First();

            if (HasComp<StaticPortalComponent>(target))
            {
                // if target is a portal, signal that they shouldn't be immediately teleported back
                AddTimeout(subject, target);
                if (pulled != null)
                    AddTimeout(pulled.Value, target);
            }

            var coords = Transform(target).Coordinates;
            TeleportEntity(ent, subject, coords, target);

            if (pulled != null)
            {
                TeleportEntity(ent, pulled.Value, coords, target, false);

                // both arrive on the same spot, so give the new pull the old rope length back
                if (_pulling.TryStartPull(subject, pulled.Value)
                    && oldJoint != null
                    && GetPullJoint(pulled.Value) is { } joint)
                {
                    joint.Length = oldJoint.Length;
                    joint.MaxLength = oldJoint.MaxLength;
                    Dirty(pulled.Value, Comp<JointComponent>(pulled.Value));
                }
            }
        }
    }

    private bool CanTakePuller(EntityUid puller)
    {
        return !_tag.HasTag(puller, PreventCollisionTag)
               && !Transform(puller).Anchored
               && !HasComp<StaticPortalTimeoutComponent>(puller)
               && !(TryComp<BuckleComponent>(puller, out var buckle) && buckle.Buckled);
    }

    private DistanceJoint? GetPullJoint(EntityUid pulled, PullableComponent? pullable = null)
    {
        if (!Resolve(pulled, ref pullable, false)
            || pullable.PullJointId == null
            || !TryComp<JointComponent>(pulled, out var joints)
            || !joints.GetJoints.TryGetValue(pullable.PullJointId, out var joint))
            return null;

        return joint as DistanceJoint;
    }

    private void AddTimeout(EntityUid subject, EntityUid exitPortal)
    {
        var timeout = EnsureComp<StaticPortalTimeoutComponent>(subject);
        timeout.ExitPortal = exitPortal;
        Dirty(subject, timeout);
    }

    // server only, so the client never lets anyone through before the server does
    [SubscribeLocalEvent]
    private void OnEndCollide(Entity<StaticPortalComponent> ent, ref EndCollideEvent args)
    {
        if (_netMan.IsClient || args.OurFixtureId != TimeoutFixture)
            return;

        if (TryComp<StaticPortalTimeoutComponent>(args.OtherEntity, out var timeout) && timeout.ExitPortal == ent.Owner)
            RemCompDeferred<StaticPortalTimeoutComponent>(args.OtherEntity);
    }

    /// <summary>
    /// Checks if the colliding fixtures are the ones we want.
    /// </summary>
    /// <returns>
    /// False if our fixture is not a portal fixture.
    /// False if other fixture is not hard, but makes an exception for projectiles.
    /// </returns>
    private bool ShouldCollide(string ourId, string otherId, Fixture our, Fixture other)
    {
        return ourId == PortalFixture && (other.Hard || otherId == ProjectileFixture);
    }

    /// <summary>
    /// Checks if the client is able to predict the teleport.
    /// </summary>
    /// <returns>
    /// False if the linked entity doesn't exist on the client / is outside PVS.
    /// </returns>
    private bool CanPredictTeleport(Entity<LinkedEntityComponent> portal)
    {
        var first = portal.Comp.LinkedEntities.First();
        return Exists(first) && Transform(first).MapID != MapId.Nullspace;
    }

    /// <summary>
    /// Handles teleporting a subject from the portal entity to a coordinate.
    /// </summary>
    /// <param name="ent"> The portal being collided with. </param>
    /// <param name="subject"> The entity getting teleported. </param>
    /// <param name="target"> The location to teleport to. </param>
    /// <param name="targetEntity"> The portal on the other side of the teleport. </param>
    private void TeleportEntity(Entity<StaticPortalComponent> ent, EntityUid subject, EntityCoordinates target, EntityUid? targetEntity = null, bool playSound = true)
    {
        var arrivalSound = CompOrNull<StaticPortalComponent>(targetEntity)?.ArrivalSound ?? ent.Comp.ArrivalSound;
        var departureSound = ent.Comp.DepartureSound;

        LogTeleport(ent, subject, Transform(subject).Coordinates, target);

        // in one side and out the other: turned by the portals' angle plus a half turn,
        // with the velocity taken relative to the entry portal and handed over to the exit
        var rotation = _transform.GetWorldRotation(subject);
        var velocity = _physics.GetMapLinearVelocity(subject) - _physics.GetMapLinearVelocity(ent);
        var mover = CompOrNull<InputMoverComponent>(subject);
        var camera = mover != null ? _mover.GetParentGridAngle(mover) : Angle.Zero;

        _transform.SetCoordinates(subject, target);

        if (targetEntity is { } exit && HasComp<StaticPortalComponent>(exit))
        {
            var turn = _transform.GetWorldRotation(exit) - _transform.GetWorldRotation(ent) + Math.PI;
            _transform.SetWorldRotation(subject, rotation + turn);

            if (mover != null && !_mover.CameraRotationLocked)
                TurnCamera((subject, mover), camera + turn);

            if (TryComp<PhysicsComponent>(subject, out var body))
            {
                var wanted = turn.RotateVec(velocity) + _physics.GetMapLinearVelocity(exit);
                var current = _physics.GetMapLinearVelocity(subject, body);
                _physics.SetLinearVelocity(subject, body.LinearVelocity + wanted - current, body: body);
            }
        }

        RaiseLocalEvent(subject, new TeleportedEvent { Subject = subject }, true); // Floofstation. Note that this is raised broadcast.

        if (!playSound)
            return;

        _audio.PlayPredicted(departureSound, ent, subject);
        _audio.PlayPredicted(arrivalSound, subject, subject);
    }

    private void TurnCamera(Entity<InputMoverComponent> ent, Angle worldAngle)
    {
        var xform = Transform(ent);
        var relative = xform.GridUid ?? xform.MapUid;
        var relativeAngle = relative is { } r ? _transform.GetWorldRotation(r) : Angle.Zero;

        ent.Comp.RelativeEntity = relative;
        ent.Comp.RelativeRotation = (worldAngle - relativeAngle).Reduced();
        ent.Comp.TargetRelativeRotation = ent.Comp.RelativeRotation;
        ent.Comp.LerpTarget = TimeSpan.Zero;
        Dirty(ent);
    }

    public void SetOpen(Entity<StaticPortalComponent?> ent, float open)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        open = Math.Clamp(open, 0f, 1f);
        var old = ent.Comp.Open;
        if (MathHelper.CloseTo(old, open))
            return;

        ent.Comp.Open = open;
        Dirty(ent);

        if (old <= 0f && open > 0f)
            _audio.PlayPredicted(ent.Comp.OpenSound, ent, null);
        else if (old > 0f && open <= 0f)
            _audio.PlayPredicted(ent.Comp.CloseSound, ent, null);
    }

    protected virtual void LogTeleport(EntityUid portal, EntityUid subject, EntityCoordinates source, EntityCoordinates target)
    {
    }
}
