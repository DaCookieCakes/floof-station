using Content.Shared.Actions;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Vehicle;

namespace Content.Shared._Floof.Vehicle;

public sealed partial class VehicleToggleActionSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<VehicleToggleActionComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnOperatorSet(Entity<VehicleToggleActionComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } old && ent.Comp.ActionEntity is { } action)
            _actions.RemoveProvidedAction(old, ent, action);

        if (args.NewOperator is { } user)
            _actions.AddAction(user, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    [SubscribeLocalEvent]
    private void OnAction(Entity<VehicleToggleActionComponent> ent, ref VehicleToggleActionEvent args)
    {
        if (args.Handled)
            return;

        _toggle.Toggle(ent.Owner, args.Performer);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnToggled(Entity<VehicleToggleActionComponent> ent, ref ItemToggledEvent args)
    {
        _actions.SetToggled(ent.Comp.ActionEntity, args.Activated);
    }
}
