using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Vehicle;

/// <summary>
///     Gives the vehicle's operator an action that toggles the vehicle's ItemToggle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleToggleActionComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}

public sealed partial class VehicleToggleActionEvent : InstantActionEvent;
