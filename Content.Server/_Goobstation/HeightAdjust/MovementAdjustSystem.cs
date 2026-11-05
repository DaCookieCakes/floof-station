// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Content.Shared._EE.HeightAdjust;
using Content.Shared._EE.Flight.Components;

namespace Content.Server._Goobstation.HeightAdjust;

public sealed partial class MovementAdjustSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FlightAffectedByScaleComponent, HeightAdjustedEvent>((uid, comp, args) => TryAdjustFlight((uid, comp), ref args));
    }

    /// <summary>
    ///     Adjusts the flight speed and stamina drain of the specified entity based on the settings provided by the component.
    /// </summary>
    public bool TryAdjustFlight(Entity<FlightAffectedByScaleComponent> ent, ref HeightAdjustedEvent args)
    {
        if (!TryComp<FlightComponent>(ent, out var flight)
            || flight.SpeedModifier == 0f
            || !_config.GetCVar(CCVars.HeightAdjustModifiesFlight))
            return false;

        args.NewScale.Deconstruct(out float xscale, out float yscale);

        var factor = (xscale + yscale) / 2;

        var staminaDrainFactor = Math.Clamp(factor, ent.Comp.MinStaminaDrainFactor, ent.Comp.MaxStaminaDrainFactor);
        var speedFactor = Math.Clamp(factor, ent.Comp.MinSpeedFactor, ent.Comp.MaxSpeedFactor);

        // Floof Section - HeightWidth
        ent.Comp.BaseSpeedModifier ??= flight.SpeedModifier;
        ent.Comp.BaseStaminaDrainRate ??= flight.StaminaDrainRate;

        flight.StaminaDrainRate = ent.Comp.BaseStaminaDrainRate.Value * staminaDrainFactor;
        flight.SpeedModifier = ent.Comp.BaseSpeedModifier.Value * speedFactor;
        Dirty(ent, flight);
        // Floof Section End

        return true;
    }
}
