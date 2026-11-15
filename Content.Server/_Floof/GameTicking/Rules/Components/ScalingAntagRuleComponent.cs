using Content.Shared.Antag;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.GameTicking.Rules.Components;

/// <summary>
/// Starts the round with zero antags and recalculates the target whenever a player joins or leaves,
/// picking eligible crew already on station to fill the gap. Never removes antags when the count drops.
/// The rule's own <c>AntagSelection</c> counts should be zero so normal selection stays out of the way.
/// </summary>
[RegisterComponent]
public sealed partial class ScalingAntagRuleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<AntagSpecifierPrototype> Antag;

    /// <summary>
    /// One antag per this many active players.
    /// </summary>
    [DataField]
    public int PlayerRatio = 10;

    [DataField]
    public int Max = 8;
}
