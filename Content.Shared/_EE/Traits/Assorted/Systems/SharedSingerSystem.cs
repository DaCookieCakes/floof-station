using Content.Shared._DV.Harpy.Components;
using Content.Shared._EE.Traits.Assorted.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Instruments;
using Content.Shared.Zombies;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._EE.Traits.Assorted.Systems;

public abstract partial class SharedSingerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<Components.SingerComponent> ent, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.ToggleActionEntity);
    }

    [SubscribeLocalEvent]
    private void OnZombified(ref EntityZombifiedEvent args)
    {
        CloseMidiUi(args.Target);
    }

    [SubscribeLocalEvent]
    private void OnBoundUIClosed(EntityUid uid, Components.SingerComponent component, BoundUIClosedEvent args)
    {
        if (args.UiKey is not InstrumentUiKey)
            return;

        TryComp(uid, out AppearanceComponent? appearance);
        _appearance.SetData(uid, HarpyVisualLayers.Singing, SingingVisualLayer.False, appearance);
    }

    [SubscribeLocalEvent]
    private void OnBoundUIOpened(EntityUid uid, Components.SingerComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is not InstrumentUiKey)
            return;

        TryComp(uid, out AppearanceComponent? appearance);
        _appearance.SetData(uid, HarpyVisualLayers.Singing, SingingVisualLayer.True, appearance);
    }

    [SubscribeLocalEvent]
    private void OnPlayerDetached(EntityUid uid, Components.SingerComponent component, PlayerDetachedEvent args)
    {
        CloseMidiUi(uid);
    }

    /// <summary>
    ///     Closes the MIDI UI if it is open. Does nothing on client side.
    /// </summary>
    public virtual void CloseMidiUi(EntityUid uid)
    {
    }

    /// <summary>
    ///     Sets up the swappable instrument on the entity, only on the server.
    /// </summary>
    protected virtual void SetUpSwappableInstrument(EntityUid uid, SingerInstrumentPrototype singer)
    {
    }
}
