using Content.Shared._EE.CCVars;
using Content.Shared._EE.OfferItem;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client._EE.OfferItem;

public sealed partial class OfferItemSystem : SharedOfferItemSystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IEyeManager _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EECVars.OfferModeIndicatorsPointShow, OnShowOfferIndicatorsChanged, true);
    }
    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay<OfferItemIndicatorsOverlay>();

        base.Shutdown();
    }

    public bool IsInOfferMode()
    {
        var entity = _playerManager.LocalEntity;

        if (entity == null)
            return false;

        return IsInOfferMode(entity.Value);
    }
    private void OnShowOfferIndicatorsChanged(bool isShow)
    {
        if (isShow)
        {
            _overlayManager.AddOverlay(new OfferItemIndicatorsOverlay(
                _inputManager,
                EntityManager,
                _eye,
                this));
        }
        else
            _overlayManager.RemoveOverlay<OfferItemIndicatorsOverlay>();
    }
}
