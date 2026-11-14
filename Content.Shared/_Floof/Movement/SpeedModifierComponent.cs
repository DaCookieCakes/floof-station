namespace Content.Shared._Floof.Movement;

/// <summary>
///     Multiplies the movement speed of the entity it's on.
/// </summary>
[RegisterComponent]
public sealed partial class SpeedModifierComponent : Component
{
    [DataField]
    public float WalkModifier = 1f;

    [DataField]
    public float SprintModifier = 1f;
}
