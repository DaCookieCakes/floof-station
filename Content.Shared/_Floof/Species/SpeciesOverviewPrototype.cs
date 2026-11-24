using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Species;

/// <summary>
/// Quick overview of a species for the character editor's species tab. The ID matches the species ID.
/// </summary>
[Prototype]
public sealed partial class SpeciesOverviewPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Guidebook entry shown in the species tab. Falls back to a guide entry with the species ID.
    /// </summary>
    [DataField]
    public ProtoId<GuideEntryPrototype>? GuideEntry;

    [DataField]
    public List<LocId> Pros = new();

    [DataField]
    public List<LocId> Cons = new();
}
