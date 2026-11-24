using Robust.Shared.Configuration;
using Content.Server.Voting.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Voting;
using Robust.Server.Player;
using Content.Server.GameTicking;
using Content.Shared._Floof.CCVars;

namespace Content.Server._Floof.AutoVote;

//Originaly from Einstien Engines, see the following pr:
//https://github.com/Simple-Station/Einstein-Engines/pull/1213
public sealed partial class AutoVoteSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public bool ShouldVoteNextJoin;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnReturnedToLobby);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
    }

    public void OnReturnedToLobby(RoundRestartCleanupEvent ev) => CallAutovote();

    public void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!ShouldVoteNextJoin)
            return;

        CallAutovote();
        ShouldVoteNextJoin = false;
    }

    private void CallAutovote()
    {
        //if we are in debug we do not want to run the auto call
#if DEBUG
        return;
#else
        if (!_cfg.GetCVar(CCVars.AutoVoteEnabled))
            return;

        if (_playerManager.PlayerCount == 0)
        {
            ShouldVoteNextJoin = true;
            return;
        }

        if (_cfg.GetCVar(CCVars.MapAutoVoteEnabled))
            _voteManager.CreateStandardVote(null, StandardVoteType.Map);
        if (_cfg.GetCVar(CCVars.PresetAutoVoteEnabled))
            _voteManager.CreateStandardVote(null, StandardVoteType.Preset);
#endif
    }
}
