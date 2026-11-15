namespace Content.Server._Floof.FathomGate;

[RegisterComponent, Access(typeof(FathomGateSystem))]
public sealed partial class FathomGateComponent : Component
{
    [DataField]
    public FathomGateState State = FathomGateState.Closed;

    [DataField]
    public float UpkeepRate = 20_000f;

    [DataField]
    public float LowCharge = 0.25f;
}

public enum FathomGateState : byte
{
    Closed,
    Open,
    Collapsed,
}
