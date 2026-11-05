namespace Content.Shared._Floof.HeightAdjust;

[RegisterComponent]
public sealed partial class HeightAdjustedFixturesComponent : Component
{
    [ViewVariables]
    public Dictionary<string, float> BaseRadii = new();
}
