namespace Content.Server._Floof.Light;

/// <summary>
///     Component that represents a backup light, it has an internal battery that charges when the power is on.
/// </summary>
[RegisterComponent, Access(typeof(BackupLightSystem))]
public sealed partial class BackupLightComponent : Component
{
    [ViewVariables]
    public BackupLightState State;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("wattage")]
    public float Wattage = 5;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("chargingWattage")]
    public float ChargingWattage = 60;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("chargingEfficiency")]
    public float ChargingEfficiency = 0.85f;
}

public enum BackupLightState : byte
{
    Charging,
    Full,
    Empty,
    On
}
