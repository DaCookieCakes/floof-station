using Content.Shared._Floof.Link;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;

namespace Content.Server._Floof.Link;

public sealed partial class StaticPortalSystem : SharedStaticPortalSystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    // TODO Move to shared
    protected override void LogTeleport(EntityUid portal, EntityUid subject, EntityCoordinates source,
        EntityCoordinates target)
    {
        if (HasComp<MindContainerComponent>(subject) && !HasComp<GhostComponent>(subject))
            _adminLogger.Add(LogType.Teleport, LogImpact.Low, $"{ToPrettyString(subject):player} teleported via {ToPrettyString(portal)} from {source} to {target}");
    }
}
