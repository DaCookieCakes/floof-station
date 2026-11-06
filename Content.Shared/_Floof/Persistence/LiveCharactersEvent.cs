using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Persistence;

/// <summary>
///     A live character as the lobby shows it.
/// </summary>
[Serializable, NetSerializable]
public sealed class LiveCharacterInfo(LiveCharacterRecord record, int? lastSavedRound)
{
    public readonly LiveCharacterRecord Record = record;
    public readonly int? LastSavedRound = lastSavedRound;
}

/// <summary>
///     Sends a player the live characters of all their slots, keyed by slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class LiveCharactersEvent(Dictionary<int, LiveCharacterInfo> characters) : EntityEventArgs
{
    public readonly Dictionary<int, LiveCharacterInfo> Characters = characters;
}

/// <summary>
///     Asks the server to discard the live character of one of the player's slots.
/// </summary>
[Serializable, NetSerializable]
public sealed class LiveCharacterDiscardRequestEvent(int slot) : EntityEventArgs
{
    public readonly int Slot = slot;
}
