using System.Linq;
using System.Numerics;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Body;

public sealed partial class OrganSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;

    /// <summary>
    /// Spawns an organ into the body and relates it to the first organ of the given parent category.
    /// </summary>
    public void AddOrgan(EntityUid body, EntProtoId<OrganComponent> organ, ProtoId<OrganCategoryPrototype> parentCategory)
    {
        if (!_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            return;

        var spawned = Spawn(organ, new EntityCoordinates(body, Vector2.Zero));
        if (!_container.Insert(spawned, container))
        {
            Del(spawned);
            return;
        }

        foreach (var other in container.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(other, out var organComp) || organComp.Category != parentCategory)
                continue;

            _organRelation.Relate(other, spawned);
            break;
        }
    }

    /// <summary>
    /// Gives the target every organ of the given categories that the source has and the target lacks,
    /// related to the target's first organ of the given parent category.
    /// </summary>
    public void CopyOrgans(EntityUid source,
        EntityUid target,
        IReadOnlyCollection<ProtoId<OrganCategoryPrototype>> categories,
        ProtoId<OrganCategoryPrototype> parentCategory)
    {
        if (!_container.TryGetContainer(source, BodyComponent.ContainerID, out var sourceContainer)
            || !_container.TryGetContainer(target, BodyComponent.ContainerID, out var targetContainer))
            return;

        var present = new HashSet<ProtoId<OrganCategoryPrototype>>();
        foreach (var organ in targetContainer.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
                present.Add(category);
        }

        foreach (var organ in sourceContainer.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || organComp.Category is not { } category
                || !categories.Contains(category)
                || present.Contains(category)
                || MetaData(organ).EntityPrototype is not { } proto)
                continue;

            AddOrgan(target, proto.ID, parentCategory);
        }
    }
}
