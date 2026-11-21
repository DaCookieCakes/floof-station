using Content.Client._Floof.Sonar;
using Content.Shared._Floof.MapLayer.Transit;
using Content.Shared._Floof.Sonar;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Floof.MapLayer.Transit;

[UsedImplicitly]
public sealed class SubConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SubConsoleWindow? _window;

    public SubConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        EntMan.EnsureComponent<SonarViewComponent>(Owner);
        _window = this.CreateWindow<SubConsoleWindow>();
        _window.OnSonarToggled +=() => SendPredictedMessage(new SonarToggleMessage());
        _window.OnLift += () => SendMessage(new TransitLiftMessage());
        _window.OnLand += () => SendMessage(new TransitLandMessage());
        _window.OnDock += () => SendMessage(new TransitDockMessage());
        _window.DockRequest += (dock, target) => SendMessage(new DockRequestMessage
        {
            DockEntity = dock,
            TargetDockEntity = target,
        });
        _window.UndockRequest += dock => SendMessage(new UndockRequestMessage { DockEntity = dock });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.Dispose();
            if (EntMan.EntityExists(Owner))
                EntMan.RemoveComponent<SonarViewComponent>(Owner);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ShuttleBoundUserInterfaceState cState)
            return;

        _window?.UpdateState(Owner, cState);
    }
}
