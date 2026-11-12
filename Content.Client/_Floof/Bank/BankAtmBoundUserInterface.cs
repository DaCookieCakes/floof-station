using Content.Shared._Floof.Bank;
using Robust.Client.UserInterface;

namespace Content.Client._Floof.Bank;

public sealed class BankAtmBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BankAtmWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BankAtmWindow>();
        _window.OnWithdraw += amount => SendMessage(new BankAtmWithdrawMessage(amount));
        Update();
    }

    /// <summary>
    /// Shows the player's own balance, which only their client knows. The ATM never sends it, so nobody nearby sees it.
    /// </summary>
    public override void Update()
    {
        base.Update();
        _window?.UpdateState(EntMan.System<BankAccountSystem>().GetLocalBalance());
    }
}
