namespace Content.Client._Floof.Movement;

/// <summary>
/// Raised on an entity after <see cref="Content.Client.Movement.Systems.ClientSpriteMovementSystem"/> has swapped
/// its movement layers, so other systems can adjust layers it doesn't know about.
/// </summary>
[ByRefEvent]
public readonly record struct SpriteMovementUpdatedEvent(bool IsMoving);
