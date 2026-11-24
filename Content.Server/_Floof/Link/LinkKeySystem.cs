using Content.Shared._Floof.Link;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._Floof.Link;

public sealed partial class LinkKeySystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _linked = default!;
    
    [SubscribeLocalEvent] //I love this tag!
    private void OnMapInit(Entity<LinkKeyComponent> ent, ref MapInitEvent args)
    {
        if (!Paused(ent)) 
            TryLinkPartner(ent);
    }
    // mapinit runs when paused, but paused entities can't link, but unpause only runs when the map was actually paused.
    // So on mapinit the link doesn't establish. This fixes that.
    [SubscribeLocalEvent]
    private void OnUnpaused(Entity<LinkKeyComponent> ent, ref EntityUnpausedEvent args)
    {
        TryLinkPartner(ent);
    }

    private void TryLinkPartner(Entity<LinkKeyComponent> ent)
    {
        if (IsLinked(ent))
            return;

        var query = EntityQueryEnumerator<LinkKeyComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == ent.Owner || other.Key != ent.Comp.Key || IsLinked(uid))
                continue;

            _linked.TryLink(ent, uid);
            return;
        }
    }

    private bool IsLinked(EntityUid uid)
    {
        return TryComp<LinkedEntityComponent>(uid, out var link) && link.LinkedEntities.Count > 0;
    }
}
