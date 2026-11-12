using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Floof.Swimming;

/// <summary>
///     A mob that wades, swims and gets carried off in deep liquid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SwimmerComponent : Component
{
    /// <summary>
    ///     Share of its speed the water leaves it.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float SpeedFactor = 1f;

    /// <summary>
    ///     The current carrying it on the grid, eased towards what the water around it reads. Each side keeps its own.
    /// </summary>
    [ViewVariables]
    public Vector2 Current;

    /// <summary>
    ///     When the last stroke started, which is also what the client bobs the sprite from.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan LastStroke;

    /// <summary>
    ///     The stroke the client last splashed for.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastSplash;

    /// <summary>
    ///     When the client next splashes for wading.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextWadeSplash;
}
