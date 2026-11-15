using Content.Shared._Floof.Link;
using Robust.Client.GameObjects;

namespace Content.Client._Floof.Link;

public sealed partial class StaticPortalSystem : SharedStaticPortalSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private PointLightSystem _light = default!;

    private const string PortalLayer = "portal";

    [SubscribeLocalEvent]
    private void OnStartup(Entity<StaticPortalComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<StaticPortalComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<StaticPortalComponent> ent)
    {
        var open = ent.Comp.Open > 0f;

        // a sprite with a frame around the portal only hides the portal layer
        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            if (_sprite.LayerMapTryGet((ent, sprite), PortalLayer, out var layer, false))
                _sprite.LayerSetVisible((ent, sprite), layer, open);
            else
                _sprite.SetVisible((ent, sprite), open);
        }

        if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, open, light);
    }
}
