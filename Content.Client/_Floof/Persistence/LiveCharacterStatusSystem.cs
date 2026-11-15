using System.Diagnostics.CodeAnalysis;
using Content.Client.Lobby;
using Content.Shared._Floof.Persistence;

namespace Content.Client._Floof.Persistence;

/// <summary>
///     Tracks the local player's live characters and which of their character slots still have a body in the round.
/// </summary>
public sealed partial class LiveCharacterStatusSystem : EntitySystem
{
    [Dependency] private IClientPreferencesManager _prefs = default!;

    private HashSet<int> _inRound = new();
    private Dictionary<int, LiveCharacterInfo> _live = new();

    /// <summary>
    ///     Raised when the server sends which slots are in the round.
    /// </summary>
    public event Action? Updated;

    /// <summary>
    ///     Raised when the server sends the live characters.
    /// </summary>
    public event Action? LiveUpdated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<LiveCharacterInRoundEvent>(OnInRound);
        SubscribeNetworkEvent<LiveCharactersEvent>(OnLive);
    }

    private void OnInRound(LiveCharacterInRoundEvent ev)
    {
        _inRound = ev.Slots;
        Updated?.Invoke();
    }

    private void OnLive(LiveCharactersEvent ev)
    {
        _live = ev.Characters;
        LiveUpdated?.Invoke();
    }

    /// <summary>
    ///     Whether the selected character's body is still in the round, so it can't join again.
    /// </summary>
    public bool SelectedInRound()
    {
        return _prefs.Preferences is { } prefs && _inRound.Contains(prefs.SelectedCharacterIndex);
    }

    public bool InRound(int slot)
    {
        return _inRound.Contains(slot);
    }

    public bool TryGetLive(int slot, [NotNullWhen(true)] out LiveCharacterInfo? info)
    {
        return _live.TryGetValue(slot, out info);
    }

    /// <summary>
    ///     Forgets a deleted slot's live character, which the server deletes along with the slot.
    /// </summary>
    public void Forget(int slot)
    {
        if (_live.Remove(slot))
            LiveUpdated?.Invoke();
    }

    public void Discard(int slot)
    {
        RaiseNetworkEvent(new LiveCharacterDiscardRequestEvent(slot));
    }
}
