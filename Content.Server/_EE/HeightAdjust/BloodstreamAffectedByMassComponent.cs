// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;

namespace Content.Server._EE.HeightAdjust;

/// <summary>
///     When applied to a humanoid or any mob, adjusts their blood volume based on their height and width.
///     <br/>
///     The formula for the resulting bloodstream volume is <code>V = BaseVolume * (Width^2 * Height)^Power</code>
///     with the factor clamped between the specified Min and Max values.
/// </summary>
[RegisterComponent]
public sealed partial class BloodstreamAffectedByMassComponent : Component
{
    /// <summary>
    ///     Minimum and maximum resulting volume factors. A minimum value of 0.5 means that the resulting volume will be at least 50% of the original.
    /// </summary>
    [DataField]
    public float Min = 1 / 3f, Max = 3f;

    /// <summary>
    ///     The power to which the mass factor will be risen.
    /// </summary>
    [DataField]
    public float Power = 1f;

    /// <summary>
    ///     The unscaled blood volume, captured on the first adjustment so repeated adjustments don't compound.
    /// </summary>
    [ViewVariables]
    public FixedPoint2? BaseVolume; // Floof - HeightWidth
}
