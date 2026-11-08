using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Ocean;

/// <summary>
///     On a map sitting underwater: whatever is open to the map is the sea. Nothing vents into it, breaches take on water
///     and anyone out in it swims, drowns and feels the pressure. Dry compartments stay as they are.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OceanSeaComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "Seawater";

    /// <summary>
    ///     Pressure of the water in kPa at the surface.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SurfacePressure = 101.325f;

    /// <summary>
    ///     Pressure of the water in kPa once deep enough. 100 kPa is about 10 meters, and barotrauma starts hurting at 550.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeepPressure = 600f;

    /// <summary>
    ///     The <see cref="OceanDepthComponent"/> depth in meters at which <see cref="DeepPressure"/> is reached. Maps
    ///     without one are always that deep.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FullPressureDepth = 240f;

    /// <summary>
    ///     Share of a puddle that has to be the sea's water before it carries the pressure.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinShare = 0.5f;

    /// <summary>
    ///     How deep a flooded tile has to be before it counts as open to the sea and takes its pressure.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float PressureDepth = 0.9f;

    /// <summary>
    ///     How fast the water comes in through each open side of a tile.
    /// </summary>
    [DataField]
    public FixedPoint2 InflowPerSecond = FixedPoint2.New(30000);
}
