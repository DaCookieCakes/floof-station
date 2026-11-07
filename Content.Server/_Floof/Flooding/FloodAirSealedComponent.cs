namespace Content.Server._Floof.Flooding;

/// <summary>
///     A puddle deep enough to seal its tile against gas.
/// </summary>
[RegisterComponent, Access(typeof(FloodAirSealSystem))]
public sealed partial class FloodAirSealedComponent : Component;
