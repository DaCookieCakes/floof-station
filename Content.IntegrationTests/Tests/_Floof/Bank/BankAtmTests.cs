using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Floof.Bank;
using Content.Server.GameTicking;
using Content.Shared._Floof.Bank;
using Content.Shared.GameTicking;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Floof.Bank;

[TestFixture]
[TestOf(typeof(BankAtmSystem))]
public sealed class BankAtmTests : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [SidedDependency(Side.Server)] private BankSystem _bank = default!;
    [SidedDependency(Side.Server)] private ServerGameTicker _ticker = default!;
    [SidedDependency(Side.Server)] private SharedUserInterfaceSystem _ui = default!;

    [Test]
    [Description("Withdrawing zero, negative or uncovered amounts changes nothing; a valid withdrawal pays out cash.")]
    public async Task TestWithdrawInvalidAmounts()
    {
        var user = Client.User!.Value;

        await Server.WaitPost(() =>
        {
            _ticker.ToggleReadyAll(true);
            _ticker.StartRound();
        });
        await RunTicksSync(10);
        Assert.That(_ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        var player = ServerSession!.AttachedEntity!.Value;

        var atm = await SpawnAtPosition("Atm", SComp<TransformComponent>(player).Coordinates);
        // The client opens the window twice if the ATM arrives in the same state as the open UI.
        await RunTicksSync(5);
        await Server.WaitPost(() => _ui.OpenUi(atm, BankAtmUiKey.Key, player));

        int CashTotal()
        {
            var total = 0;
            var query = SEntMan.EntityQueryEnumerator<StackComponent>();
            while (query.MoveNext(out var stack))
            {
                if (stack.StackTypeId == "Credit")
                    total += stack.Count;
            }
            return total;
        }

        async Task Withdraw(int amount)
        {
            await Server.WaitPost(() =>
                SEntMan.EventBus.RaiseLocalEvent(atm, new BankAtmWithdrawMessage(amount) { Actor = player, UiKey = BankAtmUiKey.Key }));
            await RunTicksSync(10);
        }

        var balance = await _bank.GetBalance(user, 0);
        Assert.That(balance, Is.Not.Null);
        var cash = 0;
        await Server.WaitPost(() => cash = CashTotal());

        foreach (var amount in new[] { 0, -1, -1000, int.MinValue, balance!.Value + 1, int.MaxValue })
        {
            await Withdraw(amount);
            Assert.That(await _bank.GetBalance(user, 0), Is.EqualTo(balance), $"withdrawing {amount} changed the balance");
            await Server.WaitPost(() => Assert.That(CashTotal(), Is.EqualTo(cash), $"withdrawing {amount} spawned cash"));
        }

        await Withdraw(100);
        Assert.That(await _bank.GetBalance(user, 0), Is.EqualTo(balance - 100));
        await Server.WaitPost(() =>
        {
            Assert.That(CashTotal(), Is.EqualTo(cash + 100));
            Assert.That(_bank.TryGetAccount(player, out var account));
            Assert.That(account.Comp.Balance, Is.EqualTo(balance - 100));
        });
    }
}
