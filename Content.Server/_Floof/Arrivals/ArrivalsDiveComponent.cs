namespace Content.Server._Floof.Arrivals;

/// <summary>
/// An arrivals shuttle that dives under the ocean to the station and rises back to the terminal, instead of FTL.
/// </summary>
[RegisterComponent, Access(typeof(ArrivalsDiveSystem))]
public sealed partial class ArrivalsDiveComponent : Component
{
    /// <summary>
    /// How long one way takes. The ocean shader is timed for this, so faster dives look wrong.
    /// </summary>
    [DataField]
    public TimeSpan DiveTime = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Tiles per second at full speed, heading where the shuttle's nose points.
    /// </summary>
    [DataField]
    public float Speed = 3f;

    /// <summary>
    /// Share of the trip spent getting up to full speed.
    /// </summary>
    [DataField]
    public float SpeedUpShare = 0.05f;

    /// <summary>
    /// Share of the trip spent slowing down to a stop.
    /// </summary>
    [DataField]
    public float SlowDownShare = 0.1f;

    /// <summary>
    /// The map the shuttle travels through, made on the first trip.
    /// </summary>
    [ViewVariables]
    public EntityUid? TravelMap;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public bool ToStation;

    [ViewVariables]
    public TimeSpan ArriveTime;

    [ViewVariables]
    public int ShakesLeft;

    [ViewVariables]
    public TimeSpan NextShake;
}
