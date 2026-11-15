using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;

namespace Content.Shared._Floof.Silicons.Borgs;

/// <summary>
/// Handles dogborg pose actions. Visuals are handled client-side by DogborgVisualsSystem.
/// </summary>
public sealed partial class DogborgSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DogborgComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DogborgComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DogborgComponent, DogborgPoseActionEvent>(OnPoseAction);
        SubscribeLocalEvent<DogborgComponent, SpriteMoveEvent>(OnSpriteMove);
        SubscribeLocalEvent<DogborgComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(Entity<DogborgComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.SitAction, ent.Comp.SitActionId);
        _actions.AddAction(ent, ref ent.Comp.RestAction, ent.Comp.RestActionId);
        _actions.AddAction(ent, ref ent.Comp.BellyUpAction, ent.Comp.BellyUpActionId);
        Dirty(ent);
    }

    private void OnShutdown(Entity<DogborgComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.SitAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.RestAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.BellyUpAction);
    }

    private void OnPoseAction(Entity<DogborgComponent> ent, ref DogborgPoseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetPose(ent, ent.Comp.Pose == args.Pose ? DogborgPose.Standing : args.Pose);
    }

    private void OnSpriteMove(Entity<DogborgComponent> ent, ref SpriteMoveEvent args)
    {
        // Walking off stands you back up.
        if (args.IsMoving)
            SetPose(ent, DogborgPose.Standing);
    }

    private void OnMobStateChanged(Entity<DogborgComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            SetPose(ent, DogborgPose.Standing);
    }

    public void SetPose(Entity<DogborgComponent> ent, DogborgPose pose)
    {
        if (ent.Comp.Pose == pose)
            return;

        ent.Comp.Pose = pose;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.SitAction, pose == DogborgPose.Sit);
        _actions.SetToggled(ent.Comp.RestAction, pose == DogborgPose.Rest);
        _actions.SetToggled(ent.Comp.BellyUpAction, pose == DogborgPose.BellyUp);

        _appearance.SetData(ent, DogborgVisuals.Pose, pose);
    }
}
