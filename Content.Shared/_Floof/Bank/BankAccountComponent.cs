using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._Floof.Bank;

/// <summary>
/// Links a character's mind to its bank account and caches the balance for UIs.
/// Minds are only sent to their own player, so nobody else sees the balance.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BankAccountComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Balance;

    [ViewVariables]
    public NetUserId UserId;

    [ViewVariables]
    public int Slot;
}
