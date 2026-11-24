using Content.Server.PDA;
using Content.Shared.CartridgeLoader;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Server._Floof.Bank;

/// <summary>
/// Issues characters' PDAs their account, keeps the balance on them up to date and delivers bank notices.
/// </summary>
public sealed partial class BankPdaSystem : EntitySystem
{
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PdaSystem _pda = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BankAccountAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<BankAccountChangedEvent>(OnChanged);
    }

    /// <summary>
    /// Only the PDA spawned for the body gets the account. Clones have none, so the original keeps working.
    /// </summary>
    private void OnAssigned(ref BankAccountAssignedEvent ev)
    {
        if (!_inventory.TryGetSlotEntity(ev.Body, "id", out var pda)
            || !TryComp<PdaComponent>(pda, out var pdaComp)
            || pdaComp.PdaOwner != ev.Body)
            return;

        var bankPda = EnsureComp<BankPdaComponent>(pda.Value);
        bankPda.UserId = ev.UserId;
        bankPda.Slot = ev.Slot;
    }

    private void OnChanged(ref BankAccountChangedEvent ev)
    {
        var query = EntityQueryEnumerator<BankPdaComponent, PdaComponent>();
        while (query.MoveNext(out var uid, out var bankPda, out var pda))
        {
            if (bankPda.UserId != ev.UserId || bankPda.Slot != ev.Slot)
                continue;

            bankPda.Balance = ev.Balance;
            _pda.UpdatePdaUi(uid, pda);

            if (ev.Notice is { } notice)
                Notify((uid, bankPda), Loc.GetString(notice, ("amount", ev.Amount), ("balance", ev.Balance)));
        }
    }

    /// <summary>
    /// Notices only reach the account's own character, holding the PDA or wearing it.
    /// </summary>
    private void Notify(Entity<BankPdaComponent> pda, string message)
    {
        if (!_container.TryGetContainingContainer((pda, null, null), out var container)
            || !_bank.TryGetAccount(container.Owner, out var account)
            || account.Comp.UserId != pda.Comp.UserId
            || account.Comp.Slot != pda.Comp.Slot
            || !TryComp<CartridgeLoaderComponent>(pda, out var loader)
            || !loader.NotificationsEnabled)
            return;

        _cartridgeLoader.SendNotification(pda, Loc.GetString("bank-pda-notification-header"), message, loader);
        _popup.PopupEntity(message, container.Owner, container.Owner);
    }

    /// <summary>
    /// Finds a PDA issued to the character.
    /// </summary>
    public bool TryGetPda(NetUserId userId, int slot, out Entity<PdaComponent> pda)
    {
        var query = EntityQueryEnumerator<BankPdaComponent, PdaComponent>();
        while (query.MoveNext(out var uid, out var bankPda, out var pdaComp))
        {
            if (bankPda.UserId != userId || bankPda.Slot != slot)
                continue;

            pda = (uid, pdaComp);
            return true;
        }

        pda = default;
        return false;
    }
}
