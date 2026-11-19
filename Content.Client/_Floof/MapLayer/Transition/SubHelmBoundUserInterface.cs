using Content.Shared._Floof.MapLayer.Transition;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Events;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Floof.MapLayer.Transition;

[UsedImplicitly]
public sealed class SubHelmBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SubHelmWindow? _window;

    public SubHelmBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        EntMan.EnsureComponent<SonarViewComponent>(Owner);
        _window = this.CreateWindow<SubHelmWindow>();
        _window.OnSonarToggled +=() => SendPredictedMessage(new SonarToggleMessage());
        _window.OnLift += () => SendMessage(new SubLiftMessage());
        _window.OnLand += () => SendMessage(new SubLandMessage());
        _window.OnDock += () => SendMessage(new SubDockMessage());
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
