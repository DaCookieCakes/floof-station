using Robust.Client.Graphics;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Draws the liquid over the lower part of objects, items and mobs standing in it.
/// </summary>
public sealed partial class FloodObjectSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new FloodObjectOverlay());
        _overlay.AddOverlay(new FloodItemOverlay());
        _overlay.AddOverlay(new FloodMobOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<FloodObjectOverlay>();
        _overlay.RemoveOverlay<FloodItemOverlay>();
        _overlay.RemoveOverlay<FloodMobOverlay>();
    }
}
