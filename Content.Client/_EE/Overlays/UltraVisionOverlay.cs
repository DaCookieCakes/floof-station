using System.Numerics;
using Content.Shared._EE.Traits.Assorted.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._EE.Overlays;

// UltraVisionOverlay.cs by Tyler Chase Johnson (also known as VMSolidus) is marked CC0 1.0.
// To view a copy of this mark, visit https://creativecommons.org/publicdomain/zero/1.0/
public sealed partial class UltraVisionOverlay : Overlay
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly ProtoId<ShaderPrototype> _overlayName = "UltraVision";

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _ultraVisionShader;

    public UltraVisionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _ultraVisionShader = _protoMan.Index(_overlayName).Instance().Duplicate();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_playerMan.LocalEntity is not { Valid: true } player
            || !_entMan.HasComponent<UltraVisionComponent>(player))
            return false;

        return base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _ultraVisionShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_ultraVisionShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}
