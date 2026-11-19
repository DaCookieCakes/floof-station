using System.Numerics;

namespace Content.Server._Floof.Arrivals;

[RegisterComponent, Access(typeof(ArrivalsDiveSystem))]
public sealed partial class ArrivalsDiveComponent : Component
{
    [DataField]
    public TimeSpan DiveTime = TimeSpan.FromSeconds(150);

    [DataField]
    public float Speed = 3f;

    [DataField]
    public float SpeedUpShare = 0.05f;
    
    [DataField]
    public float SlowDownShare = 0.1f;
    
    [ViewVariables]
    public EntityUid? TransitionLayer;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public bool ToStation;

    [ViewVariables]
    public TimeSpan ArriveTime;

    [ViewVariables]
    public Vector2 Start;

    [ViewVariables]
    public Vector2 Forward;

    [ViewVariables]
    public int ShakesLeft;

    [ViewVariables]
    public TimeSpan NextShake;
}
