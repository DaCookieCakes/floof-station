using Content.Shared.DeviceLinking.Components;
using Content.Shared.DeviceLinking.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.QuickLink;

public sealed partial class QuickLinkSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnExamined(Entity<QuickLinkComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("quick-link-examine"));
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<QuickLinkComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<SignallerComponent>(args.Used))
            return;

        args.Handled = true;

        if (_inventory.TryGetContainingSlot(ent.Owner, out _))
        {
            _popup.PopupEntity(Loc.GetString("quick-link-worn"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new QuickLinkDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<QuickLinkComponent> ent, ref QuickLinkDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } signaller)
            return;

        args.Handled = true;

        if (_deviceLink.GetLinks(signaller, ent).Count > 0)
        {
            _deviceLink.RemoveSinkFromSource(signaller, ent.Owner);
            _audio.PlayPredicted(ent.Comp.UnlinkSound, ent, args.User);
            _popup.PopupEntity(Loc.GetString("quick-link-unlinked", ("signaller", signaller)), ent, args.User);
            return;
        }

        _deviceLink.LinkDefaults(args.User, signaller, ent.Owner);
        _audio.PlayPredicted(ent.Comp.LinkSound, ent, args.User);
    }
}

[Serializable, NetSerializable]
public sealed partial class QuickLinkDoAfterEvent : SimpleDoAfterEvent;
