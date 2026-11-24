using Robust.Client.Graphics;

namespace Content.Client._Floof.Ocean;

public sealed partial class OceanBackdropSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new OceanBackdropOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<OceanBackdropOverlay>();
    }
}
