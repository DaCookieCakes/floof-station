using Content.Client._Floof.Persistence;
using Content.Client.Message;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private LiveCharacterStatusSystem _liveCharacter = default!;

    private void LiveCharacterInit()
    {
        _liveCharacter = _entManager.System<LiveCharacterStatusSystem>();
        LiveCharacterDiscard.ResetTime = TimeSpan.FromSeconds(5);
        LiveCharacterDiscard.OnPressed += _ =>
        {
            if (CharacterSlot is { } slot)
                _liveCharacter.Discard(slot);
        };
    }

    /// <summary>
    ///     Shows the live version warning if the edited slot has one.
    /// </summary>
    public void UpdateLiveCharacter()
    {
        if (CharacterSlot is not { } slot || !_liveCharacter.TryGetLive(slot, out var info))
        {
            LiveCharacterBanner.Visible = false;
            return;
        }

        LiveCharacterBanner.Visible = true;
        LiveCharacterLabel.SetMarkup(Loc.GetString("live-character-editor-warning", ("name", info.Record.Name)));

        var inRound = _liveCharacter.InRound(slot);
        LiveCharacterDiscard.Disabled = inRound;
        LiveCharacterDiscard.ToolTip = inRound ? Loc.GetString("live-character-already-in-round") : null;
    }
}
