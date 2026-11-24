using Content.Shared.Interaction;
using Content.Shared.IdentityManagement;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Player;

namespace Content.Shared._EE.OfferItem;

public abstract partial class SharedOfferItemSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OfferItemComponent, InteractUsingEvent>(SetInReceiveMode);
        SubscribeLocalEvent<OfferItemComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<OfferItemComponent, AcceptOfferAlertEvent>(OnAcceptOfferAlert);
        SubscribeLocalEvent<OfferItemComponent, InteractHandEvent>(OnInteractHand, before: [typeof(InteractionPopupSystem)]); // Floof - accept by clicking the offerer

        InitializeInteractions();
    }

    private void SetInReceiveMode(EntityUid uid, OfferItemComponent component, InteractUsingEvent args)
    {
        if (!TryComp<OfferItemComponent>(args.User, out var offerItem))
            return;

        if (args.User == uid || component.IsInReceiveMode || !offerItem.IsInOfferMode ||
            (offerItem.IsInReceiveMode && offerItem.Target != uid))
            return;

        component.IsInReceiveMode = true;
        component.Target = args.User;

        Dirty(uid, component);

        offerItem.Target = uid;
        offerItem.IsInOfferMode = false;

        Dirty(args.User, offerItem);

        if (offerItem.Item == null)
            return;

        args.Handled = true; // Floof - item offering

        _popup.PopupEntity(Loc.GetString("offer-item-try-give",
            ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
            ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
        _popup.PopupEntity(Loc.GetString("offer-item-try-give-target",
            ("user", Identity.Entity(component.Target.Value, EntityManager)),
            ("item", Identity.Entity(offerItem.Item.Value, EntityManager))), component.Target.Value, uid);
    }

    private void OnMove(EntityUid uid, OfferItemComponent component, MoveEvent args)
    {
        if (component.Target == null ||
            _transform.InRange(args.NewPosition,
                Transform(component.Target.Value).Coordinates, component.MaxOfferDistance))
            return;

        UnOffer(uid, component);
    }

    private void OnAcceptOfferAlert(EntityUid uid, OfferItemComponent component, AcceptOfferAlertEvent args)
    {
        Accept(uid, component); // Floof - accept by clicking the offerer
    }

    // Floof Section - accept by clicking the offerer
    private void OnInteractHand(EntityUid uid, OfferItemComponent component, InteractHandEvent args)
    {
        if (args.Handled ||
            !TryComp<OfferItemComponent>(args.User, out var receiver) ||
            !receiver.IsInReceiveMode ||
            receiver.Target != uid)
            return;

        args.Handled = Accept(args.User, receiver);
    }
    // Floof Section End

    private bool Accept(EntityUid uid, OfferItemComponent component) // Floof - accept by clicking the offerer
    {
        if (!TryComp<OfferItemComponent>(component.Target, out var offerItem)
            || !TryComp<HandsComponent>(uid, out var hands)
            || offerItem.Hand == null)
            return false;

        if (offerItem.Item != null)
        {
            if (!_hands.TryPickup(uid, offerItem.Item.Value, handsComp: hands))
            {
                _popup.PopupEntity(Loc.GetString("offer-item-full-hand"), uid, uid);
                return true;
            }

            _popup.PopupEntity(
                Loc.GetString(
                "offer-item-give",
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                component.Target.Value);

            _popup.PopupEntity(
                Loc.GetString(
                    "offer-item-give-other",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))),
                component.Target.Value,
                Filter.PvsExcept(component.Target.Value, entityManager: EntityManager)
                    .RemoveWhereAttachedEntity(e => e == uid), // Floof - receiver gets its own popup
                true);

            // Floof Section - receiver popup
            _popup.PopupEntity(
                Loc.GetString(
                    "offer-item-give-target",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager))),
                component.Target.Value,
                uid);
            // Floof Section End
        }

        offerItem.Item = null;
        UnReceive(uid, component, offerItem);
        return true;
    }

    /// <summary>
    /// Resets the <see cref="OfferItemComponent"/> of the user and the target
    /// </summary>
    protected void UnOffer(EntityUid uid, OfferItemComponent component)
    {
        if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHandId == null)
            return;


        if (TryComp<OfferItemComponent>(component.Target, out var offerItem) && component.Target != null)
        {

            if (component.Item != null)
            {
                _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                    ("item", Identity.Entity(component.Item.Value, EntityManager)),
                    ("target", Identity.Entity(component.Target.Value, EntityManager))), uid, uid);
                _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                    ("user", Identity.Entity(uid, EntityManager)),
                    ("item", Identity.Entity(component.Item.Value, EntityManager))), uid, component.Target.Value);
            }

            else if (offerItem.Item != null)
            {
                _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                    ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
                _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                    ("user", Identity.Entity(component.Target.Value, EntityManager)),
                    ("item", Identity.Entity(offerItem.Item.Value, EntityManager))), component.Target.Value, uid);
            }

            offerItem.IsInOfferMode = false;
            offerItem.IsInReceiveMode = false;
            offerItem.Hand = null;
            offerItem.Target = null;
            offerItem.Item = null;

            Dirty(component.Target.Value, offerItem);
        }

        component.IsInOfferMode = false;
        component.IsInReceiveMode = false;
        component.Hand = null;
        component.Target = null;
        component.Item = null;

        Dirty(uid, component);
    }


    /// <summary>
    /// Cancels the transfer of the item
    /// </summary>
    protected void UnReceive(EntityUid uid, OfferItemComponent? component = null, OfferItemComponent? offerItem = null)
    {
        if (component == null && !TryComp(uid, out component))
            return;

        if (offerItem == null && !TryComp(component.Target, out offerItem))
            return;

        if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHandId == null ||
            component.Target == null)
            return;

        if (offerItem.Item != null)
        {
            _popup.PopupEntity(Loc.GetString("offer-item-no-give",
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager)),
                ("target", Identity.Entity(uid, EntityManager))), component.Target.Value, component.Target.Value);
            _popup.PopupEntity(Loc.GetString("offer-item-no-give-target",
                ("user", Identity.Entity(component.Target.Value, EntityManager)),
                ("item", Identity.Entity(offerItem.Item.Value, EntityManager))), component.Target.Value, uid);
        }

        if (!offerItem.IsInReceiveMode)
        {
            offerItem.Target = null;
            component.Target = null;
        }

        offerItem.Item = null;
        offerItem.Hand = null;
        component.IsInReceiveMode = false;

        Dirty(uid, component);
    }

    /// <summary>
    /// Returns true if <see cref="OfferItemComponent.IsInOfferMode"/> = true
    /// </summary>
    protected bool IsInOfferMode(EntityUid? entity, OfferItemComponent? component = null)
    {
        return entity != null && Resolve(entity.Value, ref component, false) && component.IsInOfferMode;
    }


}
