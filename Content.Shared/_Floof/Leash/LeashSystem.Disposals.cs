using System.Linq;
using Content.Shared._Floof.Leash.Components;
using Content.Shared.Disposal.Unit;
using Robust.Shared.Containers;

namespace Content.Shared._Floof.Leash;

public sealed partial class LeashSystem
{
    private bool _checkDisposalLeashes;

    private void InitializeDisposals()
    {
        SubscribeLocalEvent<DisposalHolderComponent, EntInsertedIntoContainerMessage>(OnDisposalHolderInserted);
    }

    private void OnDisposalHolderInserted(Entity<DisposalHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (_net.IsServer)
            _checkDisposalLeashes = true;
    }

    private void UpdateDisposals()
    {
        if (!_checkDisposalLeashes)
            return;

        _checkDisposalLeashes = false;

        var query = EntityQueryEnumerator<LeashComponent>();
        while (query.MoveNext(out var leashEnt, out var leash))
        {
            var leashHolder = GetDisposalHolder(leashEnt);
            foreach (var data in leash.Leashed.ToList())
            {
                if (!TryGetEntity(data.Pulled, out var target))
                    continue;

                if (GetDisposalHolder(target.Value) == leashHolder)
                    continue;

                RemoveLeash(target.Value, (leashEnt, leash));
                _popups.PopupEntity(Loc.GetString("leash-snap-popup", ("leash", leashEnt)), target.Value);
            }
        }
    }

    private EntityUid? GetDisposalHolder(EntityUid uid)
    {
        if (_container.TryGetOuterContainer(uid, Transform(uid), out var outer)
            && HasComp<DisposalHolderComponent>(outer.Owner))
            return outer.Owner;

        return null;
    }
}
