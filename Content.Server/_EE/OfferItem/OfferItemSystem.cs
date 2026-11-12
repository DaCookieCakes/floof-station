using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Alert;
using Content.Shared._EE.OfferItem;

namespace Content.Server._EE.OfferItem;

public sealed partial class OfferItemSystem : SharedOfferItemSystem
{
    [Dependency] private AlertsSystem _alertsSystem = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OfferItemComponent>();
        while (query.MoveNext(out var uid, out var offerItem))
        {
            if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHandId == null)
                continue;

            if (offerItem.Hand != null &&
                _hands.GetHeldItem((uid, hands), offerItem.Hand) == null)
            {
                if (offerItem.Target != null)
                {
                    UnReceive(offerItem.Target.Value, offerItem: offerItem);
                    offerItem.IsInOfferMode = false;
                    Dirty(uid, offerItem);
                }
                else
                    UnOffer(uid, offerItem);
            }

            if (!offerItem.IsInReceiveMode)
            {
                _alertsSystem.ClearAlert(uid, offerItem.OfferAlert);
                continue;
            }

            _alertsSystem.ShowAlert(uid, offerItem.OfferAlert);
        }
    }
}
