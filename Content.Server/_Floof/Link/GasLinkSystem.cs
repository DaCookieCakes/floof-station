using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos.Components; 
using Content.Shared.NodeContainer;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._Floof.Link;

public sealed partial class GasLinkSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private LinkedEntitySystem _linked = default!;

    [SubscribeLocalEvent]
    private void OnAtmosUpdate(Entity<GasLinkComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        // yeah, both sides tick, but this is more robust than only having one side tick.
        if (!ent.Comp.Enabled
            || !_linked.GetLink(ent.Owner, out var partner)
            || !TryComp<GasLinkComponent>(partner, out var partnerLink)
            || !partnerLink.Enabled)
            return;

        if (!TryGetPipe(ent, out var pipe) || !TryGetPipe(partner.Value, out var partnerPipe))
            return;

        var a = pipe.Air;
        var b = partnerPipe.Air;
        if (a == b || a.Immutable || b.Immutable)
            return;
        
        var bShare = b.Volume / (a.Volume + b.Volume);
        _atmosphere.Merge(a, b.RemoveRatio(1f));
        _atmosphere.Merge(b, a.RemoveRatio(bShare));
    }

    public void SetEnabled(Entity<GasLinkComponent?> ent, bool enabled)
    {
        if (Resolve(ent, ref ent.Comp, false))
            ent.Comp.Enabled = enabled;
    }

    private bool TryGetPipe(EntityUid uid, [NotNullWhen(true)] out PipeNode? pipe)
    {
        pipe = null;
        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return false;

        foreach (var node in container.Nodes.Values)
        {
            if (node is not PipeNode found)
                continue;

            pipe = found;
            return true;
        }

        return false;
    }
}
