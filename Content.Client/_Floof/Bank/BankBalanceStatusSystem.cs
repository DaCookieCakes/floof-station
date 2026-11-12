using Content.Shared._Floof.Bank;

namespace Content.Client._Floof.Bank;

/// <summary>
/// Holds the balances of the local player's character slots, so the lobby can show them.
/// </summary>
public sealed partial class BankBalanceStatusSystem : EntitySystem
{
    private Dictionary<int, int> _balances = new();

    /// <summary>
    /// Raised when the server sends any balance.
    /// </summary>
    public event Action? Updated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BankBalancesEvent>(OnBalances);
        SubscribeNetworkEvent<BankBalanceChangedEvent>(OnChanged);
    }

    private void OnBalances(BankBalancesEvent ev)
    {
        _balances = ev.Balances;
        Updated?.Invoke();
    }

    private void OnChanged(BankBalanceChangedEvent ev)
    {
        _balances[ev.Slot] = ev.Balance;
        Updated?.Invoke();
    }

    /// <summary>
    /// The balance of a character slot, or null if the server hasn't sent it.
    /// </summary>
    public int? GetBalance(int slot)
    {
        return _balances.TryGetValue(slot, out var balance) ? balance : null;
    }
}
