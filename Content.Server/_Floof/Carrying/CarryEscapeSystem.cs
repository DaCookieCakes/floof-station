using Content.Server.Popups;
using Content.Shared._DV.Carrying;
using Content.Shared._Floof.Carrying;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;

namespace Content.Server._Floof.Carrying;

public sealed partial class CarryEscapeSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private CarryingSystem _carrying = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PopupSystem _popup = default!;

    private static readonly TimeSpan BaseEscapeTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MinEscapeTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxEscapeTime = TimeSpan.FromSeconds(15);

    [SubscribeLocalEvent]
    private void OnMoveInput(Entity<BeingCarriedComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        var carrier = ent.Comp.Carrier;
        if (!_actionBlocker.CanInteract(ent, carrier))
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent, GetEscapeTime(carrier, ent), new CarryEscapeDoAfterEvent(), ent, target: carrier)
        {
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("carry-escape-start-self"), ent, ent);
        _popup.PopupEntity(Loc.GetString("carry-escape-start-carrier", ("carried", ent)), carrier, carrier, PopupType.MediumCaution);
    }

    [SubscribeLocalEvent]
    private void OnEscape(Entity<BeingCarriedComponent> ent, ref CarryEscapeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target != ent.Comp.Carrier)
            return;

        args.Handled = true;
        _carrying.DropCarried(ent.Comp.Carrier, ent);
    }

    private TimeSpan GetEscapeTime(EntityUid carrier, EntityUid carried)
    {
        var time = BaseEscapeTime;
        if (TryComp<PhysicsComponent>(carrier, out var carrierPhysics)
            && TryComp<PhysicsComponent>(carried, out var carriedPhysics)
            && carriedPhysics.FixturesMass > 0)
        {
            time *= carrierPhysics.FixturesMass / carriedPhysics.FixturesMass;
        }

        if (time < MinEscapeTime)
            return MinEscapeTime;

        return time > MaxEscapeTime ? MaxEscapeTime : time;
    }
}
