using Robust.Shared.Network;

namespace Content.Server._Floof.Bank;

/// <summary>
/// A PDA issued to a character, showing its account's balance to whoever reads it.
/// Notices only go to the account's own character holding it.
/// </summary>
[RegisterComponent]
public sealed partial class BankPdaComponent : Component
{
    [ViewVariables]
    public NetUserId UserId;

    [ViewVariables]
    public int Slot;

    /// <summary>
    /// Null until the account's balance was loaded.
    /// </summary>
    [ViewVariables]
    public int? Balance;
}
