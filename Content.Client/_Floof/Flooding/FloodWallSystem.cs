using Robust.Client.Graphics;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Draws the waterline on walls, windows and closed doors next to wet tiles.
/// </summary>
public sealed partial class FloodWallSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new FloodWallOverlay());
        _overlay.AddOverlay(new FloodDoorOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<FloodWallOverlay>();
        _overlay.RemoveOverlay<FloodDoorOverlay>();
    }
}
