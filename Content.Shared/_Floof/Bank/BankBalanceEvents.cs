using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Bank;

/// <summary>
///     Sends a player the balances of all their character slots, keyed by slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class BankBalancesEvent(Dictionary<int, int> balances) : EntityEventArgs
{
    public readonly Dictionary<int, int> Balances = balances;
}

/// <summary>
///     Sends a player the new balance of one of their character slots.
/// </summary>
[Serializable, NetSerializable]
public sealed class BankBalanceChangedEvent(int slot, int balance) : EntityEventArgs
{
    public readonly int Slot = slot;
    public readonly int Balance = balance;
}
