using Content.Shared.Decals;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Decals;

/// <summary>
///     Marker that places a multi-tile decal picture (a floor logo or sign) around itself at map init, then deletes
///     itself. The marker sprite shows the picture so mappers see what they place.
/// </summary>
[RegisterComponent]
public sealed partial class DecalLayoutSpawnerComponent : Component
{
    [DataField(required: true)]
    public List<DecalLayoutEntry> Decals = new();

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public int ZIndex;

    [DataField]
    public bool? Cleanable;
}

[DataDefinition]
public sealed partial class DecalLayoutEntry
{
    [DataField(required: true)]
    public ProtoId<DecalPrototype> Decal;

    [DataField]
    public Vector2i Offset;
}
