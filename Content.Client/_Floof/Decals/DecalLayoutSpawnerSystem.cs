using Content.Shared._Floof.Decals;
using Robust.Client.GameObjects;

namespace Content.Client._Floof.Decals;

/// <summary>
///     Draws the layout on the marker sprite, so the marker looks like the picture it places.
/// </summary>
public sealed partial class DecalLayoutSpawnerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DecalLayoutSpawnerComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<DecalLayoutSpawnerComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Below the prototype's own layers, so the marker cross stays on top.
        Entity<SpriteComponent?> spriteEnt = (ent.Owner, sprite);
        var index = 0;
        foreach (var entry in ent.Comp.Decals)
        {
            if (!ProtoMan.TryIndex(entry.Decal, out var proto))
                continue;

            var layer = _sprite.AddLayer(spriteEnt, proto.Sprite, index++);
            _sprite.LayerSetOffset(spriteEnt, layer, entry.Offset);
            _sprite.LayerSetColor(spriteEnt, layer, ent.Comp.Color);
        }
    }
}
