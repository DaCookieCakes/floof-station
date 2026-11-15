using Content.Shared._Floof.Ocean;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Ocean;

/// <summary>
/// Draws the deep water and dims a map's light along with it. Everyone works depth out on their own, so the light
/// isn't networked.
/// </summary>
public sealed partial class OceanDepthSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new OceanDeepOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<OceanDeepOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<OceanDepthComponent, MapLightComponent>();
        while (query.MoveNext(out var depth, out var light))
        {
            var brightness = depth.GetBrightness(_timing.CurTime);
            var color = depth.SurfaceLight;
            light.AmbientLightColor = new Color(color.R * brightness, color.G * brightness, color.B * brightness);
        }
    }
}
