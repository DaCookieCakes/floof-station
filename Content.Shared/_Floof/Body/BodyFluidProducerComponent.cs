using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Body;

[RegisterComponent, AutoGenerateComponentState, AutoGenerateComponentPause, NetworkedComponent]

public sealed partial class BodyFluidProducerComponent : Component
{
    /// <summary>
    ///     The reagent to produce.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> ReagentId = new();

    /// <summary>
    ///     The name of <see cref="Solution"/>.
    /// </summary>
    [DataField]
    public string SolutionName = SolutionComponent.DefaultSolutionId;
    
    /// <summary>
    ///     The solution to add reagent to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<SolutionComponent>? Solution = null;
    
    /// <summary>
    ///     The amount of reagent to be generated on update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 QuantityPerUpdate = 10;
    
    /// <summary>
    /// If the entity's hunger satiation is below this value, it cannot spin web.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public SatiationValue MinHungerThreshold;
    
    /// <summary>
    ///     The amount of nutrient consumed on update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HungerUsage = 1f;
    
    /// <summary>
    ///     The amount of nutrient consumed on update.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ThirstUsage = 2f;
    
    /// <summary>
    ///     How long to wait before producing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan GrowthDelay = TimeSpan.FromMinutes(1);
    
    /// <summary>
    ///     When to next try to produce.
    /// </summary>
    [DataField, AutoPausedField, Access(typeof(BodyFluidProducerSystem))]
    public TimeSpan NextGrowth = TimeSpan.Zero;
}