using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Keeps pouring liquid onto its tile.
/// </summary>
[RegisterComponent]
public sealed partial class FloodSourceComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Water";

    [DataField]
    public FixedPoint2 UnitsPerSecond = FixedPoint2.New(30000);
}
