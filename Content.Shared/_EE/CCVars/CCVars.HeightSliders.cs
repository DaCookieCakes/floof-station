// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether height & width sliders adjust a character's Fixture Component
    /// </summary>
    public static readonly CVarDef<bool> HeightAdjustModifiesHitbox =
        CVarDef.Create("heightadjust.modifies_hitbox", true, CVar.SERVERONLY);

    /// <summary>
    ///     Whether height & width sliders adjust a player's bloodstream volume.
    /// </summary>
    /// <remarks>
    ///     This can be configured more precisely by modifying BloodstreamAffectedByMassComponent.
    /// </remarks>
    public static readonly CVarDef<bool> HeightAdjustModifiesBloodstream =
        CVarDef.Create("heightadjust.modifies_bloodstream", true, CVar.SERVERONLY);

    /// <summary>
    ///     Whether height & width sliders adjust a harpy's flight speed and stamina drain.
    /// <remarks>
    ///     This can be configured more precisely by modifying FlightAffectedByScaleComponent.
    /// </remarks>
    public static readonly CVarDef<bool> HeightAdjustModifiesFlight =
        CVarDef.Create("heightadjust.modifies_flight", true, CVar.SERVERONLY);
}
