using System.Numerics;

namespace Content.Server._Floof.Arrivals;

[RegisterComponent, Access(typeof(ArrivalsDiveSystem)), AutoGenerateComponentPause]
public sealed partial class ArrivalsDivingComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public bool ToStation;

    [ViewVariables, AutoPausedField]
    public TimeSpan StartTime;

    [ViewVariables, AutoPausedField]
    public TimeSpan ArriveTime;

    [ViewVariables]
    public Vector2 Start;

    [ViewVariables]
    public Vector2 End;
}
