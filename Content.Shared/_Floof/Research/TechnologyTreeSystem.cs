using System.Diagnostics.CodeAnalysis;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Research;

/// <summary>
/// Applies <see cref="TechnologyPositionPrototype"/>s to the tech tree.
/// The tree's prerequisites are written onto the technology prototypes themselves,
/// so every upstream research check (availability, unlocking, descriptions) follows the tree without base edits.
/// </summary>
public sealed partial class TechnologyTreeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly Dictionary<ProtoId<TechnologyPrototype>, TechnologyPositionPrototype> _positions = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        LoadTree();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<TechnologyPrototype>() || args.WasModified<TechnologyPositionPrototype>())
            LoadTree();
    }

    private void LoadTree()
    {
        _positions.Clear();
        foreach (var position in _prototype.EnumeratePrototypes<TechnologyPositionPrototype>())
        {
            if (!_prototype.TryIndex(position.Technology, out var technology))
                continue;

            if (!_positions.TryAdd(position.Technology, position))
            {
                Log.Error($"Technology {position.Technology} has more than one {nameof(TechnologyPositionPrototype)}, ignoring {position.ID}");
                continue;
            }

            technology.TechnologyPrerequisites = new(position.Prerequisites);
        }
    }

    /// <summary>
    /// Gets where a technology sits on the tech tree, if it is on it at all.
    /// </summary>
    public bool TryGetPosition(ProtoId<TechnologyPrototype> technology, [NotNullWhen(true)] out TechnologyPositionPrototype? position)
    {
        return _positions.TryGetValue(technology, out position);
    }
}
