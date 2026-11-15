using System.Numerics;
using Content.Shared._Floof.Body;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Traits.Effects;

/// <summary>
/// Effect that adds components to the player entity.
/// Components are added without overwriting existing ones.
/// </summary>
public sealed partial class AddOrganEffect : BaseTraitEffect
{
    
    [DataField(required: true)]
    public required Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId<OrganComponent>> Organs;
    
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, HashSet<ProtoId<OrganCategoryPrototype>>>? Relationships;
    
    public override void Apply(TraitEffectContext ctx)
    {
        var transform = ctx.EntMan.System<SharedTransformSystem>();
        var hidelayer = ctx.EntMan.System<SharedHideableHumanoidLayersSystem>();
        var containerSystem = ctx.EntMan.System<SharedContainerSystem>();
        var organRelation = ctx.EntMan.System<OrganRelationSystem>();
        var var = ctx.EntMan.System<SharedVisualBodySystem>();
        
        if (!ctx.EntMan.TryGetComponent<ContainerManagerComponent>(ctx.Player, out var containerComp))
            return;
        
        if (!containerSystem.TryGetContainer(ctx.Player, BodyComponent.ContainerID, out var container, containerComp))
            return;
        
        
        var coords = transform.ToMapCoordinates(new EntityCoordinates(ctx.Player, Vector2.Zero));
        var spawned = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        
        foreach (var (organCategory, proto) in Organs)
        {
            var spawn = ctx.EntMan.Spawn(proto.Id, coords);
            
            if (!containerSystem.Insert(spawn, container, containerXform: ctx.Transform))
            {
                ctx.EntMan.DeleteEntity(spawn);
                continue;
            }
            spawned[organCategory] = spawn;
        }
        
        if (Relationships is null)
            return;

        foreach (var (parentCategory, childCategorySet) in Relationships)
        {
            Entity<ParentOrganComponent?>? parentOrgan = null;
            foreach (var organ in container.ContainedEntities)
            {
                var organComp = ctx.EntMan.GetComponentOrNull<OrganComponent>(organ);
                var parentOrganComp = ctx.EntMan.GetComponentOrNull<ParentOrganComponent>(organ);
                if (organComp != null && organComp.Category == parentCategory)
                {
                    parentOrgan = (organ, parentOrganComp);
                }
            }

            if (parentOrgan is not { } parent)
                continue;
            
            foreach (var childOrganCategory in childCategorySet)
            {
                if (!spawned.TryGetValue(childOrganCategory, out var childUid))
                    continue;
                
                organRelation.Relate(parent, childUid);
            }
        }
        
        if (!ctx.EntMan.TryGetComponent<ProfileTrackerComponent>(ctx.Player, out var profileTracker))
            return;
        var.ApplyMarkings(ctx.Player, profileTracker.Markings);

        // Yes this is horrible, I'll scrap the whole system later anyway so fuck it, for a week it can stay terrible.
        hidelayer.SetLayerOcclusion(ctx.Player, HumanoidVisualLayers.Breasts, true, SlotFlags.INNERCLOTHING);
        hidelayer.SetLayerOcclusion(ctx.Player, HumanoidVisualLayers.Vagina, true, SlotFlags.INNERCLOTHING);
        hidelayer.SetLayerOcclusion(ctx.Player, HumanoidVisualLayers.Penis, true, SlotFlags.INNERCLOTHING);
    }
}