namespace Content.Shared.Ghost.Components;

/// <summary>
/// This is used for marking entities which should receive all local chat message, even when out of range
/// </summary>
[RegisterComponent]
public sealed partial class GhostHearingComponent : Component
{
    /// <summary>
    /// Floof - If false, this entity will never receive any local messages
    /// </summary>
    [DataField]
    public bool CanHearLocal { get; set; } = true;
}
