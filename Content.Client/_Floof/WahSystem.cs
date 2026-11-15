using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Shared.Body.Systems;
/*
public sealed partial class WahSystem : EntitySystem
{
    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private SharedHideableHumanoidLayersSystem _hide = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnBodyInit);
    }

    private void OnBodyInit(Entity<BodyComponent> ent, ref OrganInsertedIntoEvent args)
    {

        if (!TryComp<VisualOrganMarkingsComponent>(args.Organ, out var visualOrganMarkingsComponent))
            return;
        
        if (!TryComp<HideableHumanoidLayersComponent>(ent.Owner, out var comp))
            return;

        foreach (var markings in visualOrganMarkingsComponent.Markings.Values)
        {
            foreach (var marking in markings)
            {
                if (!_marking.TryGetMarking(marking, out var proto))
                    continue;

               //if (proto.BodyPart != args.Args.Layer && !(visualOrganMarkingsComponent.DependentHidingLayers.TryGetValue(args.Args.Layer, out var dependent) && dependent.Contains(proto.BodyPart)))
               //    continue;

                foreach (var sprite in proto.Sprites)
                {
                    if (sprite is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var layerId = $"{proto.ID}-{rsi.RsiState}";

                    if (!_sprite.LayerMapTryGet(ent.Owner, layerId, out var index, false))
                        continue;

                    _sprite.LayerSetVisible(ent.Owner, index, false);
                }
            }
        }
    }
            
}
*/    

