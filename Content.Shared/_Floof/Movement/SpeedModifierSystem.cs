using Content.Shared.Movement.Systems;

namespace Content.Shared._Floof.Movement;

public sealed partial class SpeedModifierSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<SpeedModifierComponent> ent, ref ComponentStartup args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<SpeedModifierComponent> ent, ref ComponentShutdown args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRefresh(Entity<SpeedModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // still attached while shutting down
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.ModifySpeed(ent.Comp.WalkModifier, ent.Comp.SprintModifier);
    }
}
