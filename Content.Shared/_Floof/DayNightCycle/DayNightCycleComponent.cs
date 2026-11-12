using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.DayNightCycle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DayNightCycleComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<DayNightCyclePrototype> Cycle;

    /// <summary>
    /// Time of day the map starts at, 0.25 is morning. Random if not set.
    /// </summary>
    [DataField]
    public float? StartTime;

    /// <summary>
    /// Set on map init so the day starts at <see cref="StartTime"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Offset;
}
