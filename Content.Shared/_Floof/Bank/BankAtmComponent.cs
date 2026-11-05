using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Bank;

/// <summary>
/// Withdraw-only ATM: pays out cash from the user's bank account. Deposits are deliberately not supported.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BankAtmComponent : Component
{
    [DataField]
    public ProtoId<StackPrototype> CashType = "Credit";

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg", AudioParams.Default.WithVolume(-10f));

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg", AudioParams.Default.WithVolume(-10f));

    /// <summary>
    /// How long the printing animation plays after a withdrawal.
    /// </summary>
    [DataField]
    public TimeSpan PrintDuration = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan? PrintingEnd;
}

[Serializable, NetSerializable]
public enum BankAtmVisuals : byte
{
    Printing,
}

[Serializable, NetSerializable]
public enum BankAtmVisualLayers : byte
{
    Printing,
}

[Serializable, NetSerializable]
public enum BankAtmUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BankAtmWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}
