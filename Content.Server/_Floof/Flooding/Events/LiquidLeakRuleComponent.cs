using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.Flooding.Events;

/// <summary>
///     Liquid twin of the gas leak: a random station tile spills one weighted profile's reagent mix for a while.
/// </summary>
[RegisterComponent, Access(typeof(LiquidLeakRule))]
public sealed partial class LiquidLeakRuleComponent : Component
{
    [DataField(required: true)]
    public List<LiquidLeakProfile> Profiles = new();

    /// <summary>
    ///     Leak rate range in litres per second, unless the profile has its own.
    /// </summary>
    [DataField]
    public float MinimumRate = 20f;

    [DataField]
    public float MaximumRate = 60f;

    /// <summary>
    ///     Leak duration range in seconds.
    /// </summary>
    [DataField]
    public float MinimumDuration = 60f;

    [DataField]
    public float MaximumDuration = 180f;

    /// <summary>
    ///     Seconds between spills.
    /// </summary>
    [DataField]
    public float LeakCooldown = 1f;

    [DataField]
    public float TimeUntilLeak;

    [DataField]
    public EntityUid? TargetGrid;

    [DataField]
    public Vector2i TargetTile;

    [DataField]
    public LiquidLeakProfile? Profile;

    [DataField]
    public float LitresPerSecond;
}

[DataDefinition]
public sealed partial class LiquidLeakProfile
{
    /// <summary>
    ///     Reagents and their ratios.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<ReagentPrototype>, float> Reagents = new();

    [DataField]
    public float Weight = 1f;

    [DataField]
    public float? MinimumRate;

    [DataField]
    public float? MaximumRate;
}
