using Content.Client.Physics;
using Robust.Client.Graphics;

namespace Content.Client._Floof.Leash;

public sealed partial class LeashVisualsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new LeashVisualsOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<LeashVisualsOverlay>();
    }
}
