using Content.Client.Administration.Managers;
using Content.Shared._Floof.Ghost;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Ghost.Widgets;

// Floof - Ghost respawn
public sealed partial class GhostGui
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    private void InitializeRespawn()
    {
        RespawnButton.OnPressed += _ => _entMan.EntityNetManager.SendSystemNetworkMessage(new GhostRespawnRequestEvent());
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entMan.TryGetComponent<GhostRespawnComponent>(_player.LocalEntity, out var respawn)
            || respawn.RespawnTime is not { } time)
        {
            RespawnButton.Visible = false;
            return;
        }

        var left = _admin.IsActive() ? TimeSpan.Zero : time - _timing.CurTime;
        RespawnButton.Visible = true;
        RespawnButton.Disabled = left > TimeSpan.Zero;
        RespawnButton.Text = left > TimeSpan.Zero
            ? Loc.GetString("ghost-gui-respawn-button-wait", ("time", left.ToString(@"m\:ss")))
            : Loc.GetString("ghost-gui-respawn-button");
    }
}
