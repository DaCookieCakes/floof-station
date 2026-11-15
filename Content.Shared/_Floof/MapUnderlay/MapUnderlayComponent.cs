using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.MapUnderlay;

/// <summary>
///     Shows another map beneath this one, wherever this map has nothing of its own to draw. A point on this map lies
///     at <see cref="Rotation"/> turned, plus <see cref="Offset"/>, on the one below.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MapUnderlayComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Map;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField, AutoNetworkedField]
    public Angle Rotation;

    /// <summary>
    ///     Looks around the map below for whoever sees it from here, so it gets sent to them and loaded.
    /// </summary>
    [ViewVariables]
    public EntityUid? Eye;

    public Vector2 ToBelow(Vector2 position)
    {
        return Rotation.RotateVec(position) + Offset;
    }
}

/// <summary>
///     Raised on a map with <see cref="MapUnderlayComponent"/> to ask how much of the map below shows right now, and
///     how far out it looks. It's left fully showing unless something says otherwise.
/// </summary>
/// <param name="Range">Asks as if it showed from this many times further away, so it can be sent ahead.</param>
[ByRefEvent]
public record struct MapUnderlayShowEvent(float Range = 1f)
{
    /// <summary>
    ///     0 is hidden, 1 is plain to see.
    /// </summary>
    public float Share = 1f;

    /// <summary>
    ///     How much more of the map below fits on screen than of this one.
    /// </summary>
    public float Zoom = 1f;
}
