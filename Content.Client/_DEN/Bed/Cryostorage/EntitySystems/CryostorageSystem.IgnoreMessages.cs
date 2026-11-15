using Content.Shared._DEN.Bed.Cryostorage;
using Content.Shared._DEN.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;

namespace Content.Client.Bed.Cryostorage;

/// <summary>
/// Handles ignoring cryo dispatch messages.
/// </summary>
public sealed partial class CryostorageSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        
        Subs.CVar(_cfg, DenCCVars.IgnoreCryoMessage, SetIgnoreCryoMessage);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnEvent);
    }

    // Ensure we inform the server when we spawn so that it has an entity to put the component on.
    private void OnPlayerSpawnEvent(PlayerSpawnCompleteEvent evt)
    {
        RaiseNetworkEvent(new IgnoreCryoMessage(_cfg.GetCVar(DenCCVars.IgnoreCryoMessage)));
    }

    // Handles live updating of the config.
    private void SetIgnoreCryoMessage(bool state)
    {
        RaiseNetworkEvent(new IgnoreCryoMessage(state));
    }
}