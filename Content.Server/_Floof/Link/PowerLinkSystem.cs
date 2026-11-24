using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;

namespace Content.Server._Floof.Link;

public sealed partial class PowerLinkSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _linked = default!;
    [Dependency] private PowerNetSystem _powerNet = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Network load lists are only rebuilt in PowerNetSystem.Update; reading stats before that hits freed loads.
        UpdatesAfter.Add(typeof(PowerNetSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PowerLinkComponent, PowerConsumerComponent, PowerSupplierComponent>();
        while (query.MoveNext(out var uid, out var link, out var consumer, out var supplier))
        {
            Entity<PowerLinkComponent, PowerConsumerComponent, PowerSupplierComponent> ent = (uid, link, consumer, supplier);

            if (link.Direction == PowerLinkDirection.Output)
            {
                // the input end sets our supply
                consumer.DrawRate = 0f;
                if (!TryGetActivePartner(ent, out _))
                    supplier.MaxSupply = 0f;
                continue;
            }

            supplier.MaxSupply = 0f;

            if (!TryGetActivePartner(ent, out var partner))
            {
                consumer.DrawRate = 0f;
                continue;
            }

            var partnerSupplier = partner.Comp3;
            partnerSupplier.MaxSupply = consumer.ReceivedPower;

            var unmet = 0f;
            if (partnerSupplier.Net is { } net)
            {
                var stats = _powerNet.GetNetworkStatistics(net.NetworkNode);
                unmet = Math.Max(0f, stats.Consumption - stats.SupplyCurrent - stats.SupplyBatteries);
            }
            //only relying on the headroom here would lead to brownouts when power surges. This adjusts instantly to the demand.
            consumer.DrawRate = partnerSupplier.CurrentSupply + unmet + link.Headroom;
        }
    }

    /// <summary>
    /// Gets the partner if the pair can move power: both enabled, opposite directions, on different networks.
    /// </summary>
    private bool TryGetActivePartner(
        Entity<PowerLinkComponent, PowerConsumerComponent, PowerSupplierComponent> ent,
        out Entity<PowerLinkComponent, PowerConsumerComponent, PowerSupplierComponent> partner)
    {
        partner = default;

        if (!_linked.GetLink(ent, out var other)
            || !TryComp<PowerLinkComponent>(other, out var otherLink)
            || !TryComp<PowerConsumerComponent>(other, out var otherConsumer)
            || !TryComp<PowerSupplierComponent>(other, out var otherSupplier))
            return false;

        if (!ent.Comp1.Enabled || !otherLink.Enabled || ent.Comp1.Direction == otherLink.Direction)
            return false;

        // prevent power cycle
        if (ent.Comp2.Net == otherConsumer.Net)
            return false;

        partner = (other.Value, otherLink, otherConsumer, otherSupplier);
        return true;
    }

    public void SetDirection(Entity<PowerLinkComponent?> ent, PowerLinkDirection direction)
    {
        if (Resolve(ent, ref ent.Comp, false))
            ent.Comp.Direction = direction;
    }

    public void SetEnabled(Entity<PowerLinkComponent?> ent, bool enabled)
    {
        if (Resolve(ent, ref ent.Comp, false))
            ent.Comp.Enabled = enabled;
    }

    [SubscribeLocalEvent]
    private void OnLinkChanged(Entity<PowerLinkComponent> ent, ref LinkedEntityChangedEvent args)
    {
        // NewLinks contains all links not just new ones. 
        if (args.NewLinks.Count > 0 || !TryComp<PowerSupplierComponent>(ent, out var supplier))
            return;

        supplier.MaxSupply = 0f;
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<PowerLinkComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("power-link-verb-toggle-direction"),
            Act = () =>
            {
                SetDirection(ent.AsNullable(), ent.Comp.Direction == PowerLinkDirection.Input ? PowerLinkDirection.Output : PowerLinkDirection.Input);
                _popup.PopupEntity(Loc.GetString("power-link-direction-set", ("direction", ent.Comp.Direction.ToString())), ent, user);
            },
        });
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<PowerLinkComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("power-link-examine", ("direction", ent.Comp.Direction.ToString())));
    }
}
