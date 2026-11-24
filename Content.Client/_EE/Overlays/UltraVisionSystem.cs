using Content.Shared._EE.Traits.Assorted.Components;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._EE.Overlays;

// UltraVisionSystem.cs by Tyler Chase Johnson (also known as VMSolidus) is marked CC0 1.0.
// To view a copy of this mark, visit https://creativecommons.org/publicdomain/zero/1.0/
public sealed partial class UltraVisionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ISharedPlayerManager _playerMan = default!;

    private UltraVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new UltraVisionOverlay();
    }

    [SubscribeLocalEvent]
    private void OnUltraVisionInit(EntityUid uid, UltraVisionComponent component, ComponentInit args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.AddOverlay(_overlay);
    }

    [SubscribeLocalEvent]
    private void OnUltraVisionShutdown(EntityUid uid, UltraVisionComponent component, ComponentShutdown args)
    {
        if (uid != _playerMan.LocalEntity)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }

    [SubscribeLocalEvent]
    private void OnPlayerAttached(EntityUid uid, UltraVisionComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    [SubscribeLocalEvent]
    private void OnPlayerDetached(EntityUid uid, UltraVisionComponent component, LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }
}
