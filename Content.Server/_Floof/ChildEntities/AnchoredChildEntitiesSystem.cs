// copied from ss14Starlight/space-station-14 Content.Shared/_Starlight/ChildEntities/ChildEntitiesSystem.cs (Starlight License, see LICENSE-Starlight.txt).
// SPDX-License-Identifier: LicenseRef-Starlight AND AGPL-3.0-or-later

namespace Content.Server._Floof.ChildEntities;

public sealed partial class AnchoredChildEntitiesSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<AnchoredChildEntitiesComponent> ent, ref MapInitEvent args)
    {
        if (Transform(ent).Anchored)
            SpawnChildren(ent);
    }

    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<AnchoredChildEntitiesComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (LifeStage(ent) < EntityLifeStage.MapInitialized)
            return;

        DeleteChildren(ent);

        if (args.Anchored)
            SpawnChildren(ent);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<AnchoredChildEntitiesComponent> ent, ref ComponentShutdown args)
    {
        DeleteChildren(ent);
    }

    private void SpawnChildren(Entity<AnchoredChildEntitiesComponent> ent)
    {
        var xform = Transform(ent);
        foreach (var child in ent.Comp.ChildPrototypes)
        {
            var coords = xform.Coordinates.Offset(xform.LocalRotation.RotateVec(child.Offset));
            ent.Comp.Children.Add(SpawnAttachedTo(child.Prototype, coords, null, xform.LocalRotation));
        }

        var ev = new AnchoredChildrenSpawnedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void DeleteChildren(Entity<AnchoredChildEntitiesComponent> ent)
    {
        foreach (var child in ent.Comp.Children)
        {
            if (!TerminatingOrDeleted(child))
                QueueDel(child);
        }

        ent.Comp.Children.Clear();
    }
}

/// <summary>
/// Raised on the parent after it spawned its children.
/// </summary>
[ByRefEvent]
public readonly record struct AnchoredChildrenSpawnedEvent;
