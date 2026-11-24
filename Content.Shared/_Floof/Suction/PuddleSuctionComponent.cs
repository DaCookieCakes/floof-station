using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Floof.Suction;

/// <summary>
///     Sucks up nearby puddles into its own tank.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PuddleSuctionComponent : Component
{
    [DataField]
    public string Solution = "suction";

    [DataField]
    public float Range = 1.5f;

    /// <summary>
    ///     Taken from each puddle in range per second, split across the updates.
    /// </summary>
    [DataField]
    public FixedPoint2 UnitsPerTile = FixedPoint2.New(300);

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}
