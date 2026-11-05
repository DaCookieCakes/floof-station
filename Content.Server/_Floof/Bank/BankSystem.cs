using System.Threading;
using System.Threading.Tasks;
using Content.Server._Floof.Persistence;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Shared._Floof.Bank;
using Content.Shared._Floof.CCVars;
using Content.Shared.Database;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Floof.Bank;

/// <summary>
/// Personal bank accounts, one per character slot. The balance lives in its own table so profile saves never touch it.
/// The minds of characters tracked by persistence get a <see cref="BankAccountComponent"/> caching their balance.
/// </summary>
public sealed partial class BankSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    /// <summary>
    /// One lock per account, so concurrent changes can't overdraw it and balances reach entities in order.
    /// Only kept while an operation on the account is running or waiting.
    /// </summary>
    private readonly Dictionary<(NetUserId, int), AccountLock> _locks = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LiveCharacterComponent, LiveCharacterTrackedEvent>(OnTracked);
        SubscribeLocalEvent<BankAccountChangedEvent>(OnChanged);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame)
            SendBalances(args.Session);
    }

    /// <summary>
    /// Sends the lobby the balances of all the player's slots, so it can show them without loading a character.
    /// </summary>
    private async void SendBalances(ICommonSession session)
    {
        var balances = await _db.GetBankBalancesAsync(session.UserId, _cfg.GetCVar(CCVars.BankStartingBalance));
        if (session.Status == SessionStatus.Disconnected)
            return;

        RaiseNetworkEvent(new BankBalancesEvent(balances), session);
    }

    private void OnChanged(ref BankAccountChangedEvent ev)
    {
        if (_player.TryGetSessionById(ev.UserId, out var session))
            RaiseNetworkEvent(new BankBalanceChangedEvent(ev.Slot, ev.Balance), session);
    }

    /// <summary>
    /// The account belongs to the character's mind, so it follows the character into clones and borgs.
    /// </summary>
    private void OnTracked(Entity<LiveCharacterComponent> ent, ref LiveCharacterTrackedEvent args)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out _))
        {
            Log.Error($"Tracked character {ToPrettyString(ent)} has no mind to hold its bank account");
            return;
        }

        var (userId, slot) = (ent.Comp.User, ent.Comp.Slot);
        var query = EntityQueryEnumerator<BankAccountComponent>();
        while (query.MoveNext(out var uid, out var old))
        {
            if (uid != mindId && old.UserId == userId && old.Slot == slot)
                RemCompDeferred<BankAccountComponent>(uid);
        }

        var account = EnsureComp<BankAccountComponent>(mindId);
        account.UserId = userId;
        account.Slot = slot;

        var ev = new BankAccountAssignedEvent(ent, userId, slot);
        RaiseLocalEvent(ref ev);

        LoadBalance(userId, slot);
    }

    private async void LoadBalance(NetUserId userId, int slot)
    {
        await Locked(userId, slot, 0, null, () => GetBalance(userId, slot));
    }

    public Task<int?> GetBalance(NetUserId userId, int slot)
    {
        return _db.GetBankBalanceAsync(userId, slot, _cfg.GetCVar(CCVars.BankStartingBalance));
    }

    /// <summary>
    /// Finds the bank account of the mob's mind.
    /// </summary>
    public bool TryGetAccount(EntityUid mob, out Entity<BankAccountComponent> account)
    {
        account = default;
        if (!_mind.TryGetMind(mob, out var mindId, out _) || !TryComp<BankAccountComponent>(mindId, out var comp))
            return false;

        account = (mindId, comp);
        return true;
    }

    /// <summary>
    /// Adds money to an account. Returns the new balance, or null if the slot doesn't exist.
    /// </summary>
    /// <param name="notice">Sent to the character through their PDA, with the amount and balance.</param>
    public async Task<int?> Deposit(NetUserId userId, int slot, int amount, string reason, LocId? notice = null)
    {
        var balance = await Locked(userId, slot, amount, notice,
            () => _db.AddBankBalanceAsync(userId, slot, amount, _cfg.GetCVar(CCVars.BankStartingBalance)));
        if (balance != null)
            _adminLog.Add(LogType.Bank, LogImpact.Low, $"{userId} slot {slot} received {amount} ({reason}), balance {balance.Value}");

        return balance;
    }

    /// <summary>
    /// Takes money from an account. Returns the new balance, or null if there isn't enough.
    /// </summary>
    public async Task<int?> Withdraw(Entity<BankAccountComponent> ent, int amount)
    {
        var (userId, slot) = (ent.Comp.UserId, ent.Comp.Slot);
        var balance = await Locked(userId, slot, -amount, null,
            () => _db.AddBankBalanceAsync(userId, slot, -amount, _cfg.GetCVar(CCVars.BankStartingBalance)));
        if (balance != null)
            _adminLog.Add(LogType.Bank, LogImpact.Low, $"{userId} slot {slot} withdrew {amount}, balance {balance.Value}");

        return balance;
    }

    /// <summary>
    /// Runs one database operation on an account at a time, and updates its entities with the resulting balance.
    /// </summary>
    private async Task<int?> Locked(NetUserId userId, int slot, int amount, LocId? notice, Func<Task<int?>> operation)
    {
        if (!_locks.TryGetValue((userId, slot), out var accountLock))
            _locks[(userId, slot)] = accountLock = new AccountLock();

        // Continuations run on the game thread, so the counter needs no synchronization.
        accountLock.Users++;
        await accountLock.Semaphore.WaitAsync();
        try
        {
            var balance = await operation();
            if (balance != null)
                UpdateAccounts(userId, slot, balance.Value, amount, notice);

            return balance;
        }
        finally
        {
            accountLock.Semaphore.Release();
            if (--accountLock.Users == 0)
            {
                _locks.Remove((userId, slot));
                accountLock.Semaphore.Dispose();
            }
        }
    }

    private void UpdateAccounts(NetUserId userId, int slot, int balance, int amount, LocId? notice)
    {
        var query = EntityQueryEnumerator<BankAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            if (account.UserId != userId || account.Slot != slot)
                continue;

            account.Balance = balance;
            Dirty(uid, account);
        }

        var ev = new BankAccountChangedEvent(userId, slot, balance, amount, notice);
        RaiseLocalEvent(ref ev);
    }

    private sealed class AccountLock
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int Users;
    }
}

/// <summary>
/// Broadcast when a body became the character holding an account.
/// </summary>
[ByRefEvent]
public readonly record struct BankAccountAssignedEvent(EntityUid Body, NetUserId UserId, int Slot);

/// <summary>
/// Broadcast after an account's balance was loaded or changed, in the order the changes happened.
/// </summary>
/// <param name="Notice">A message for the character, formatted with the amount and balance.</param>
[ByRefEvent]
public readonly record struct BankAccountChangedEvent(NetUserId UserId, int Slot, int Balance, int Amount, LocId? Notice);
