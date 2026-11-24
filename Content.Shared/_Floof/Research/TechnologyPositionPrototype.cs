using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Research;

/// <summary>
/// Places a <see cref="TechnologyPrototype"/> on the tech tree in the research console.
/// Kept separate from the technology itself so upstream technology prototypes stay untouched.
/// </summary>
[Prototype]
public sealed partial class TechnologyPositionPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The technology being placed on the tree.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TechnologyPrototype> Technology;

    /// <summary>
    /// Grid position of the technology in the console menu.
    /// </summary>
    [DataField(required: true)]
    public Vector2i Position;

    /// <summary>
    /// Technologies that must be unlocked before this one.
    /// Replaces <see cref="TechnologyPrototype.TechnologyPrerequisites"/> entirely, see <see cref="TechnologyTreeSystem"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<TechnologyPrototype>> Prerequisites = new();
}
