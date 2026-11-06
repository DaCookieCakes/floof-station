namespace Content.Shared._Floof.Buckle;

[RegisterComponent]
public sealed partial class DirectionalBedComponent : Component
{
    /// <summary>
    /// Lie face up with the head towards the strap's back, in all four directions (lounge chairs).
    /// </summary>
    [DataField]
    public bool HeadAtBack;
}
