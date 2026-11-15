// SingerSystem.cs by Tyler Chase Johnson (also known as VMSolidus) is marked CC0 1.0. To view a copy of this mark, visit https://creativecommons.org/publicdomain/zero/1.0/

using Content.Server.Instruments;
using Content.Shared._EE.Traits.Assorted.Components;
using Content.Shared._EE.Traits.Assorted.Prototypes;
using Content.Shared._EE.Traits.Assorted.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Instruments;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Speech.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Zombies;
using Robust.Shared.Player;

namespace Content.Server._EE.Traits.Assorted;

public sealed partial class SingerSystem : SharedSingerSystem
{
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private InstrumentSystem _instrument = default!;

    [SubscribeLocalEvent]
    private void OnStartup(Entity<SingerComponent> ent, ref ComponentStartup args)
    {
        if (!ProtoMan.TryIndex(ent.Comp.Proto, out var singer))
            return;

        _actionsSystem.AddAction(ent, ref ent.Comp.ToggleActionEntity, ent.Comp.MidiActionId);

        var instrumentComp = EnsureComp<InstrumentComponent>(ent);
        var defaultData = singer.InstrumentList[singer.DefaultInstrument];
        _instrument.SetInstrumentProgram(ent.Owner, instrumentComp, defaultData.Item1, defaultData.Item2);
        SetUpSwappableInstrument(ent, singer);

        Dirty(ent, instrumentComp);
    }

    protected override void SetUpSwappableInstrument(EntityUid uid, SingerInstrumentPrototype singer)
    {
        if (singer.InstrumentList.Count <= 1)
            return;

        var swappableComp = EnsureComp<SwappableInstrumentComponent>(uid);
        swappableComp.InstrumentList = singer.InstrumentList;
    }

    [SubscribeLocalEvent]
    private void OnEquip(GotEquippedEvent args)
    {
        // Check if an item that makes the singer mumble is equipped to their face
        // (not their pockets!). As of writing, this should just be the muzzle.
        if (HasComp<MumbleAccentComponent>(args.Equipment))
            CloseMidiUi(args.EquipTarget);
    }

    [SubscribeLocalEvent]
    private void OnMobStateChangedEvent(EntityUid uid, InstrumentComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            CloseMidiUi(args.Target);
    }

    [SubscribeLocalEvent]
    private void OnKnockedDown(EntityUid uid, InstrumentComponent component, ref KnockedDownEvent args)
    {
        CloseMidiUi(uid);
    }

    [SubscribeLocalEvent]
    private void OnStunned(EntityUid uid, InstrumentComponent component, ref StunnedEvent args)
    {
        CloseMidiUi(uid);
    }

    [SubscribeLocalEvent]
    private void OnSleep(EntityUid uid, InstrumentComponent component, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            CloseMidiUi(uid);
    }

    [SubscribeLocalEvent]
    private void OnStatusEffect(EntityUid uid, InstrumentComponent component, StatusEffectAddedEvent args)
    {
        if (args.Key == "Muted")
            CloseMidiUi(uid);
    }

    /// <summary>
    /// Almost a copy of Content.Server.Damage.ForceSay.DamageForceSaySystem.OnDamageChanged.
    /// Done so because DamageForceSaySystem doesn't output an event, and my understanding is
    /// that we don't want to change upstream code more than necessary to avoid merge conflicts
    /// and maintenance overhead. It still reuses the values from DamageForceSayComponent, so
    /// any tweaks to that will keep ForceSay consistent with singing interruptions.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnDamageChanged(EntityUid uid, InstrumentComponent instrumentComponent, DamageChangedEvent args)
    {
        if (!TryComp<DamageForceSayComponent>(uid, out var component) ||
            args.DamageDelta == null ||
            !args.DamageIncreased ||
            args.DamageDelta.GetTotal() < component.DamageThreshold ||
            component.ValidDamageGroups == null)
            return;

        var totalApplicableDamage = FixedPoint2.Zero;
        foreach (var (group, value) in args.DamageDelta.GetDamagePerGroup(ProtoMan))
        {
            if (!component.ValidDamageGroups.Contains(group))
                continue;

            totalApplicableDamage += value;
        }

        if (totalApplicableDamage >= component.DamageThreshold)
            CloseMidiUi(uid);
    }

    /// <summary>
    /// Prevent the player from opening the MIDI UI under some circumstances.
    /// </summary>
    [SubscribeLocalEvent(before: [typeof(ActivatableUISystem)])]
    private void OnInstrumentOpen(EntityUid uid, SingerComponent component, OpenUiActionEvent args)
    {
        // CanSpeak covers all reasons you can't talk, including being incapacitated
        // (crit/dead), asleep, or for any reason mute inclding glimmer or a mime's vow.
        // why the fuck is any of this hardcoded?
        var canNotSpeak = !_actionBlocker.CanSpeak(uid);
        var zombified = HasComp<ZombieComponent>(uid);
        var muzzled = _inventory.TryGetSlotEntity(uid, "mask", out var maskUid) &&
                      HasComp<MumbleAccentComponent>(maskUid);

        // Set this event as handled when the singer should be incapable of singing in order
        // to stop the ActivatableUISystem event from opening the MIDI UI.
        args.Handled = canNotSpeak || muzzled || zombified;

        // Tell the user that they can not sing.
        if (args.Handled)
            _popup.PopupEntity(Loc.GetString("no-sing-while-no-speak"), uid, uid, PopupType.Medium);
    }

    public override void CloseMidiUi(EntityUid uid)
    {
        if (HasComp<ActiveInstrumentComponent>(uid) &&
            TryComp<ActorComponent>(uid, out var actor))
            _instrument.ToggleInstrumentUi(uid, actor.PlayerSession.AttachedEntity ?? EntityUid.Invalid);
    }
}
