using Content.Shared._Floof.MapLayer;
using Robust.Shared.Map.Components;

namespace Content.Client._Floof.MapLayer;

public sealed partial class MapLayerLightSystem : EntitySystem
{
    [Dependency] private MapLayerSystem _layers = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<MapLayerTransitionComponent, MapLayerComponent, MapLightComponent>();
        while (query.MoveNext(out var uid, out _, out var layer, out var light))
        {
            if (!_layers.TryGetZ((uid, layer), out var z) ||
                !_layers.TryGetStack(uid, out var stack) ||
                !_layers.TryGetBracket<MapLightComponent>(stack, z, out var above, out var below, out var t))
                continue;

            light.AmbientLightColor = Color.InterpolateBetween(below.Comp.AmbientLightColor, above.Comp.AmbientLightColor, t);
        }
    }
}
