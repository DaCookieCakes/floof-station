using Content.Shared._Floof.Bank;
using Content.Shared.Mind;
using Robust.Client.Player;

namespace Content.Client._Floof.Bank;

/// <summary>
/// Reads the local player's balance off their mind, the only one the client receives.
/// </summary>
public sealed partial class BankAccountSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BankAccountComponent, AfterAutoHandleStateEvent>(OnState);
    }

    /// <summary>
    /// The local player's balance, or null if their character has no bank account.
    /// </summary>
    public int? GetLocalBalance()
    {
        return _player.LocalEntity is { } user
            && _mind.TryGetMind(user, out var mindId, out _)
            && TryComp<BankAccountComponent>(mindId, out var account)
            ? account.Balance
            : null;
    }

    private void OnState(Entity<BankAccountComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var query = EntityQueryEnumerator<BankAtmComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (_ui.TryGetOpenUi((uid, ui), BankAtmUiKey.Key, out var bui))
                bui.Update();
        }
    }
}
