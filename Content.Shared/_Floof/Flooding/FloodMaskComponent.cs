using Robust.Shared.Utility;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Replaces the straight waterline on this entity's sprite with hand-drawn water, for oddly shaped things.
/// </summary>
/// <remarks>
///     The mask is drawn on top of the sprite, so it should match its size and directions. A pixel's green channel is how solid
///     the water gets there, 0 stays dry. Its red channel is the water level that pixel gets wet at: black straight
///     away, white only in a full tile. The colour comes from the liquid.
///     Without a mask, the whole sprite gets wet in any water. For flat things like paper and carpets, this costs no texture.
/// </remarks>
[RegisterComponent]
public sealed partial class FloodMaskComponent : Component
{
    [DataField]
    public SpriteSpecifier? Sprite;

    /// <summary>
    ///     Other states of the mask's RSI, used while the sprite shows the state they're keyed by, like an open door.
    /// </summary>
    [DataField]
    public Dictionary<string, string> Looks = new();

    /// <summary>
    ///     For corner-smoothed sprites like tables: the state prefix of the corner layers. Each of them gets the mask state of its own name.
    /// </summary>
    [DataField]
    public string? Corners;

    /// <summary>
    ///     How high the top of this surface is, as a share of a full tile. Things resting on it get wet that much later.
    /// </summary>
    [DataField]
    public float Surface;
}
