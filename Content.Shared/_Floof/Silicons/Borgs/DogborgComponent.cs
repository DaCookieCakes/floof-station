using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Silicons.Borgs;

/// <summary>
/// Marks a borg chassis as a dogborg, giving it sit/rest/belly-up poses and dogborg sprite handling.
/// </summary>
/// <remarks>
/// Sprite states are derived from the selected borg type's <c>spriteBodyState</c> (e.g. <c>k9</c>):
/// <c>k9_walk</c>, <c>k9_walk_e</c>, <c>k9_walk_l</c>, <c>k9-sit</c>, <c>k9-rest</c>, <c>k9-bellyup</c>.
/// Missing states fall back to the standing ones.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DogborgSystem))]
public sealed partial class DogborgComponent : Component
{
    [DataField, AutoNetworkedField]
    public DogborgPose Pose = DogborgPose.Standing;

    [DataField]
    public EntProtoId SitActionId = "ActionDogborgSit";

    [DataField]
    public EntProtoId RestActionId = "ActionDogborgRest";

    [DataField]
    public EntProtoId BellyUpActionId = "ActionDogborgBellyUp";

    [DataField, AutoNetworkedField]
    public EntityUid? SitAction;

    [DataField, AutoNetworkedField]
    public EntityUid? RestAction;

    [DataField, AutoNetworkedField]
    public EntityUid? BellyUpAction;
}

[Serializable, NetSerializable]
public enum DogborgPose : byte
{
    Standing,
    Sit,
    Rest,
    BellyUp,
}

[Serializable, NetSerializable]
public enum DogborgVisuals : byte
{
    Pose,
}

/// <summary>
/// Raised by the dogborg pose actions. Using the action for the current pose stands the dogborg back up.
/// </summary>
public sealed partial class DogborgPoseActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public DogborgPose Pose;
}
