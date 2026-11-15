using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Floof.Silicons.Borgs;

/// <summary>
/// Put on an item borg module to refill some of its hands over time, e.g. the security module's zipties.
/// Refills one empty hand per <see cref="RechargeTime"/>, only while the module is selected.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BorgHandRechargeComponent : Component
{
    /// <summary>
    /// Indices into the module's <c>ItemBorgModule.hands</c> list that get refilled.
    /// </summary>
    [DataField(required: true)]
    public List<int> Hands = new();

    /// <summary>
    /// What to spawn into an empty hand.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When the next empty hand gets refilled. Null while all hands are full.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextRecharge;
}
