namespace Content.Server._Floof.Arrivals;

/// <summary>
/// Latejoiner whose arrival gets announced once they first stand on one of the station's grids.
/// </summary>
[RegisterComponent, Access(typeof(ArrivalAnnouncementSystem))]
public sealed partial class PendingArrivalAnnouncementComponent : Component
{
    [DataField]
    public EntityUid Station;

    [DataField]
    public string JobName = string.Empty;

    /// <summary>
    /// Uses the gold "on deck" announcement for command and other notable jobs.
    /// </summary>
    [DataField]
    public bool Special;
}
