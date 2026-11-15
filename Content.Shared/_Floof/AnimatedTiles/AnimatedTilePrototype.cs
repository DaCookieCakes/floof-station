using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.AnimatedTiles;

/// <summary>
/// Animates the tile with the same ID. The client draws the animation over the tile.
/// </summary>
[Prototype]
public sealed partial class AnimatedTilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ResPath Sprite;

    [DataField(required: true)]
    public string State = default!;
}
