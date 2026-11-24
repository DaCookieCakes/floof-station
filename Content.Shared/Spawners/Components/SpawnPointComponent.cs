using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Spawners.Components;

[RegisterComponent]
public sealed partial class SpawnPointComponent : Component, ISpawnPoint
{
    /// <summary>
    /// The job this spawn point is valid for.
    /// Null will allow all jobs to spawn here.
    /// </summary>
    [DataField("job_id")]
    public ProtoId<JobPrototype>? Job;

    // Floofstation - lets one spawner serve several jobs (e.g. cyborg spawners also spawn dogborgs)
    /// <summary>
    /// Additional jobs this spawn point is valid for, on top of <see cref="Job"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> ExtraJobs = new();
    // Floofstation end

    /// <summary>
    /// The type of spawn point.
    /// </summary>
    [DataField("spawn_type"), ViewVariables(VVAccess.ReadWrite)]
    public SpawnPointType SpawnType { get; set; } = SpawnPointType.Unset;

    public override string ToString()
    {
        return $"{Job} {SpawnType}";
    }
}

public enum SpawnPointType
{
    Unset = 0,
    LateJoin,
    Job,
    Observer,
}
