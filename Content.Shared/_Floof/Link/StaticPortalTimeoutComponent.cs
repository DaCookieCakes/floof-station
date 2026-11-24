using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Link;

/// <summary>
///     Attached to an entity after portal transit so it isn't immediately portaled back.
///     Removed once it leaves the exit portal's timeout fixture.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StaticPortalTimeoutComponent : Component
{
    [ViewVariables, DataField, AutoNetworkedField]
    public EntityUid? ExitPortal;
}
