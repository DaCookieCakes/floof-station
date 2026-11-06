// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Traits;

/// <summary>
/// Sent from server to client when a player spawns with traits that were disabled due to unmet conditions.
/// </summary>
[Serializable, NetSerializable]
public sealed class DisabledTraitsEvent(Dictionary<ProtoId<TraitPrototype>, List<string>> disabledTraits)
    : EntityEventArgs
{
    /// <summary>
    /// Dictionary mapping disabled trait IDs to lists of reasons why they were disabled.
    /// </summary>
    public Dictionary<ProtoId<TraitPrototype>, List<string>> DisabledTraits = disabledTraits;
}
