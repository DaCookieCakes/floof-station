using Content.Shared._Floof.Silicons.Borgs;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Silicons.Borgs;

/// <summary>
/// Refills empty hands of borg modules with <see cref="BorgHandRechargeComponent"/>.
/// </summary>
public sealed partial class BorgHandRechargeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BorgHandRechargeComponent, BorgModuleComponent>();
        while (query.MoveNext(out var uid, out var recharge, out var module))
        {
            if (module.InstalledEntity is not { } chassis
                || !TryComp<HandsComponent>(chassis, out var hands)
                || FindEmptyHand((uid, recharge), (chassis, hands)) is not { } handId)
            {
                // Nothing to refill, or the module isn't selected so its hands don't exist.
                recharge.NextRecharge = null;
                continue;
            }

            if (recharge.NextRecharge is not { } next)
            {
                recharge.NextRecharge = now + recharge.RechargeTime;
                continue;
            }

            if (now < next)
                continue;

            var item = Spawn(recharge.Item, Transform(chassis).Coordinates);
            if (!_hands.TryPickup(chassis, item, handId, checkActionBlocker: false, animate: false, handsComp: hands))
                Del(item);

            recharge.NextRecharge = null;
        }
    }

    private string? FindEmptyHand(Entity<BorgHandRechargeComponent> module, Entity<HandsComponent> chassis)
    {
        foreach (var index in module.Comp.Hands)
        {
            // Matches the hand ids made by SharedBorgSystem.ProvideItems.
            var handId = $"{GetNetEntity(module.Owner)}-hand-{index}";
            if (_hands.TryGetHand(chassis.AsNullable(), handId, out _) && _hands.HandIsEmpty(chassis.AsNullable(), handId))
                return handId;
        }

        return null;
    }
}
