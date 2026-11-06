using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Floof.Bank;
using Content.Server.GameTicking;
using Content.Shared._Floof.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._Floof.Bank;

[TestFixture]
[TestOf(typeof(PaydaySystem))]
public sealed class PaydayTests : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [SidedDependency(Side.Server)] private BankSystem _bank = default!;
    [SidedDependency(Side.Server)] private IConfigurationManager _cfg = default!;
    [SidedDependency(Side.Server)] private MobStateSystem _mobState = default!;
    [SidedDependency(Side.Server)] private ServerGameTicker _ticker = default!;

    [Test]
    [Description("Paydays pay only for time spent alive, and stop once the round has ended.")]
    public async Task TestPayday()
    {
        var user = Client.User!.Value;
        var start = _cfg.GetCVar(CCVars.BankStartingBalance);

        await OverrideCVar(Side.Server, CCVars.BankAliveCheckInterval, 1f);
        await OverrideCVar(Side.Server, CCVars.BankChecksPerPayday, 2);
        await Server.WaitPost(() =>
        {
            _ticker.ToggleReadyAll(true);
            _ticker.StartRound();
        });
        await RunTicksSync(10);
        Assert.That(_ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        var player = ServerSession!.AttachedEntity!.Value;

        // Alive for the whole shift: full pay.
        await RunSeconds(2.5f);
        var balance = await _bank.GetBalance(user, 0);
        Assert.That(balance, Is.GreaterThan(start));

        // Dead for the whole shift: nothing.
        await Server.WaitPost(() => _mobState.ChangeMobState(player, MobState.Dead));
        await RunSeconds(2f);
        Assert.That(await _bank.GetBalance(user, 0), Is.EqualTo(balance));

        // Alive again, but the round has ended: no more paydays.
        await Server.WaitPost(() =>
        {
            _mobState.ChangeMobState(player, MobState.Alive);
            _ticker.EndRound();
        });
        await RunSeconds(2.5f);
        Assert.That(await _bank.GetBalance(user, 0), Is.EqualTo(balance));
    }
}
