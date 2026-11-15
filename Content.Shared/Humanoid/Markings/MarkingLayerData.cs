using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Markings;

/// <summary>
///     A representation of properties associated with a single layer of a marking.
/// </summary>
[DataDefinition]
[Serializable]
public sealed partial record MarkingLayerData
{
    /// <summary>
    ///     The sprite associated with this marking layer.
    /// </summary>
    [IncludeDataField]
    public SpriteSpecifier.Rsi Sprite = default!;

    /// <summary>
    ///     A localization ID for this marking layer.
    /// </summary>
    [DataField]
    public LocId? Name = null;

    /// <summary>
    ///     The default coloring that this layer uses.
    /// </summary>
    [DataField]
    public LayerColoringDefinition? Coloring = null;

    /// <summary>
    ///     If this is true, then this layer cannot be recolored in the character editor.
    /// </summary>
    /// <remarks>
    ///     This field is redundant if ForcedColoring is enabled on the marking.
    /// </remarks>
    [DataField]
    public bool ForcedColoring = false;

    /// <summary>
    ///     Optional field allowing assignment of shader to this layer.
    /// </summary>
    [DataField]
    public string? Shader = null;

    // Floof Section - marking layering
    /// <summary>
    ///     Optional humanoid layer to draw this sprite on instead of the marking's body part,
    ///     e.g. the part of a tail that goes behind the body.
    /// </summary>
    [DataField]
    public HumanoidVisualLayers? Layer = null;

    /// <summary>
    ///     Optional state of another sprite in this marking whose colour this one uses.
    ///     Linked sprites are hidden from the colour picker.
    /// </summary>
    [DataField]
    public string? ColorFrom = null;
    // Floof Section End

    /// <summary>
    ///     Tries to get a player-friendly name for this marking layer.
    /// </summary>
    /// <param name="markingId">The ID of the marking this layer belongs to.</param>
    public string GetLayerName(string markingId)
    {
        var locId = GetLocId(markingId);
        return Loc.GetString(locId);
    }

    /// <summary>
    ///     Gets a locale ID for this marking layer. Uses <see cref="Name"/> by default,
    ///     otherwise uses an implicit key based on the marking ID and the layer ID.
    /// </summary>
    /// <param name="markingId">The ID of the marking this layer belongs to.</param>
    public string GetLocId(string markingId)
    {
        return Name ?? $"marking-{markingId}-{GetLayerStateId()}";
    }

    /// <summary>
    ///     Gets an ID associated with this marking layer. Used for sprite keys.
    /// </summary>
    /// <param name="markingId">The ID of the marking this layer belongs to.</param>
    public string GetLayerID(string markingId)
    {
        return $"{markingId}-{GetLayerStateId()}";
    }

    /// <summary>
    ///     Get the ID associated with this marking's layer state.
    /// </summary>
    public string GetLayerStateId()
    {
        return Sprite.RsiState;
    }
}
