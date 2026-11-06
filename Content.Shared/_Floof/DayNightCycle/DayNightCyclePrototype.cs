using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.DayNightCycle;

/// <summary>
/// Map light colours over a day, from 0 (midnight) to 1.
/// </summary>
[Prototype]
public sealed partial class DayNightCyclePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public TimeSpan Length = TimeSpan.FromMinutes(30);

    [DataField(required: true)]
    public List<DaytimeColor> Colors = new();
}

[DataDefinition]
public sealed partial class DaytimeColor
{
    [DataField(required: true)]
    public float Time;

    [DataField]
    public Color Color = Color.Black;
}
