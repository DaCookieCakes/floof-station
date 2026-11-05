using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Persistence;

/// <summary>
///     Tells a player which of their character slots still have a body in the round, so the lobby can block joining as them.
/// </summary>
[Serializable, NetSerializable]
public sealed class LiveCharacterInRoundEvent(HashSet<int> slots) : EntityEventArgs
{
    public readonly HashSet<int> Slots = slots;
}
