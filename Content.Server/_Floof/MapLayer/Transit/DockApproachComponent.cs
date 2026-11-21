using System.Numerics;

namespace Content.Server._Floof.MapLayer.Transit;

[RegisterComponent]
public sealed partial class DockApproachComponent : Component
{
    [DataField]
    public Vector2 StartPosition;

    [DataField]
    public Angle StartRotation;

    [DataField]
    public Vector2 StartVelocity;

    [DataField]
    public float StartAngularVelocity;

    [DataField]
    public Vector2 EndPosition;

    [DataField]
    public Angle EndRotation;

    [DataField]
    public TimeSpan StartTime;

    [DataField]
    public TimeSpan Duration;

    [ViewVariables]
    public List<(EntityUid Sub, EntityUid Seabed)> Docks = new();
}
