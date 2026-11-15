using System.Globalization;
using Content.Server.Chat.Systems;
using Content.Shared.Station.Components;

namespace Content.Server._Floof.Arrivals;

/// <summary>
/// Announces latejoiners when they set foot on the station instead of when they spawn,
/// so people riding the arrivals shuttle aren't announced before they actually arrive.
/// </summary>
public sealed partial class ArrivalAnnouncementSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PendingArrivalAnnouncementComponent, EntParentChangedMessage>(OnParentChanged);
    }

    /// <summary>
    /// Announces the arrival right away if the mob is already on the station, otherwise once it gets there.
    /// </summary>
    public void QueueAnnouncement(EntityUid mob, EntityUid station, string jobName, bool special)
    {
        var pending = EnsureComp<PendingArrivalAnnouncementComponent>(mob);
        pending.Station = station;
        pending.JobName = jobName;
        pending.Special = special;

        TryAnnounce((mob, pending, Transform(mob)));
    }

    private void OnParentChanged(Entity<PendingArrivalAnnouncementComponent> ent, ref EntParentChangedMessage args)
    {
        TryAnnounce((ent, ent.Comp, args.Transform));
    }

    private void TryAnnounce(Entity<PendingArrivalAnnouncementComponent, TransformComponent> ent)
    {
        var (uid, pending, xform) = ent;

        if (!TryComp<StationMemberComponent>(xform.GridUid, out var member) || member.Station != pending.Station)
            return;

        RemCompDeferred<PendingArrivalAnnouncementComponent>(uid);

        var job = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(pending.JobName);
        var message = Loc.GetString(pending.Special
                ? "latejoin-arrival-announcement-special"
                : "latejoin-arrival-announcement",
            ("character", Name(uid)),
            ("entity", uid),
            ("job", job));

        _chat.DispatchStationAnnouncement(pending.Station,
            message,
            Loc.GetString("latejoin-arrival-sender"),
            playDefaultSound: false,
            colorOverride: pending.Special ? Color.Gold : null);
    }
}
