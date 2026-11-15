using Content.Server.Stack;
using Content.Shared._Floof.Bank;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Bank;

public sealed partial class BankAtmSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BankAtmComponent, BankAtmWithdrawMessage>(OnWithdraw);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BankAtmComponent>();
        while (query.MoveNext(out var uid, out var atm))
        {
            if (atm.PrintingEnd is not { } end || _timing.CurTime < end)
                continue;

            atm.PrintingEnd = null;
            _appearance.SetData(uid, BankAtmVisuals.Printing, false);
        }
    }

    private void OnWithdraw(Entity<BankAtmComponent> ent, ref BankAtmWithdrawMessage args)
    {
        Withdraw(ent, args.Actor, args.Amount);
    }

    private async void Withdraw(Entity<BankAtmComponent> ent, EntityUid user, int amount)
    {
        if (!_bank.TryGetAccount(user, out var account))
        {
            Deny(ent, user, "bank-atm-no-account");
            return;
        }

        if (amount <= 0)
        {
            Deny(ent, user, "bank-atm-invalid-amount");
            return;
        }

        if (amount > account.Comp.Balance)
        {
            Deny(ent, user, "bank-atm-insufficient-funds");
            return;
        }

        var balance = await _bank.Withdraw(account, amount);
        var gone = TerminatingOrDeleted(ent) || TerminatingOrDeleted(user);
        if (balance == null)
        {
            if (!gone)
                Deny(ent, user, "bank-atm-insufficient-funds");
            return;
        }

        // Lost power or the user walked off while the bank was busy: no cash to hand out, so give the money back.
        if (gone || !_ui.IsUiOpen(ent.Owner, BankAtmUiKey.Key, user))
        {
            await _bank.Deposit(account.Comp.UserId, account.Comp.Slot, amount, "ATM withdrawal refund");
            return;
        }

        foreach (var cash in _stack.SpawnMultipleAtPosition(ent.Comp.CashType, amount, Transform(user).Coordinates))
        {
            _hands.PickupOrDrop(user, cash);
        }

        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        ent.Comp.PrintingEnd = _timing.CurTime + ent.Comp.PrintDuration;
        _appearance.SetData(ent, BankAtmVisuals.Printing, true);
        _popup.PopupEntity(Loc.GetString("bank-atm-withdrawn", ("amount", amount)), user, user);
    }

    private void Deny(Entity<BankAtmComponent> ent, EntityUid user, string message)
    {
        _audio.PlayPvs(ent.Comp.ErrorSound, ent);
        _popup.PopupEntity(Loc.GetString(message), user, user);
    }
}
