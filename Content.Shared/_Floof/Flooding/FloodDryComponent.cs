namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Liquid isn't drawn on this entity. For things standing on the edge of a tile, which the water doesn't climb like a
///     wall or an object in the middle of it.
/// </summary>
[RegisterComponent]
public sealed partial class FloodDryComponent : Component;
