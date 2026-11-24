using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Swimming;

/// <summary>
///     Outer clothing too heavy to swim in: the wearer goes under at swimming depth and strokes at reduced speed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SwimSinkingComponent : Component;
