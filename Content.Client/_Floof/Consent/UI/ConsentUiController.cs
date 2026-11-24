using Content.Client._Floof.Consent.UI.Windows;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;

namespace Content.Client._Floof.Consent.UI;

/// <summary>
/// This was copied 1:1 from https://github.com/Fansana/floofstation1/pull/4/
/// </summary>
[UsedImplicitly]
public sealed partial class ConsentUiController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private IInputManager _input = default!;

    private ConsentWindow? _window;

    private MenuButton? ConsentButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.ConsentButton;

    public void OnStateEntered(GameplayState state)
    {
        EnsureWindow();

        _input.SetInputCommand(ContentKeyFunctions.OpenConsentWindow,
            InputCmdHandler.FromDelegate(_ => ToggleWindow()));
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.Dispose();
            _window = null;
        }
    }

    public void UnloadButton()
    {
        if (ConsentButton == null)
            return;

        ConsentButton.OnPressed -= ConsentButtonPressed;
    }

    public void LoadButton()
    {
        if (ConsentButton == null)
            return;

        ConsentButton.OnPressed += ConsentButtonPressed;
    }

    private void ConsentButtonPressed(BaseButton.ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<ConsentWindow>();
        _window.OnOpen += () =>
        {
            if (ConsentButton is not null)
                ConsentButton.Pressed = true;
            _window.UpdateUi();
        };
        _window.OnClose += () =>
        {
            if (ConsentButton is not null)
                ConsentButton.Pressed = false;
        };
    }

    private void ToggleWindow()
    {
        if (_window is null)
            return;

        UIManager.ClickSound();
        if (_window.IsOpen != true)
            _window.OpenCentered();
        else
            _window.Close();
    }
}
