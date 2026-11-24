using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Flooding;

/// <summary>
///     Mapping marker: at round start, fills the room it's in with liquid, then deletes itself.
///     The room ends at anything airtight, so closed doors hold the water and open ones let it through.
/// </summary>
[RegisterComponent]
public sealed partial class FloodMarkerComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Water";

    /// <summary>
    ///     How deep the water stands, 0 to 1 of a tile's height.
    /// </summary>
    [DataField]
    public float Depth = 0.5f;

    /// <summary>
    ///     Stops a marker in a leaky room from filling the whole station.
    /// </summary>
    [DataField]
    public int MaxTiles = 1000;
}
