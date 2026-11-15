// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using Content.Shared._EE.HeightAdjust;
using Robust.Shared.Configuration;

namespace Content.Server._EE.HeightAdjust;

public sealed partial class BloodstreamAdjustSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IConfigurationManager _config = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BloodstreamAffectedByMassComponent, HeightAdjustedEvent>((uid, comp, args) => TryAdjustBloodstream((uid, comp), args.NewScale));
    }

    /// <summary>
    ///     Adjusts the bloodstream of the specified entity based on the settings provided by the component.
    /// </summary>
    public bool TryAdjustBloodstream(Entity<BloodstreamAffectedByMassComponent> ent, Vector2 scale)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream)
            || bloodstream.BloodReferenceSolution.Volume == 0
            || !_config.GetCVar(CCVars.HeightAdjustModifiesBloodstream))
            return false;

        // Floof Section - HeightWidth
        ent.Comp.BaseVolume ??= bloodstream.BloodReferenceSolution.Volume;

        var mass = scale.X * scale.X * scale.Y;
        var factor = Math.Clamp(MathF.Pow(mass, ent.Comp.Power), ent.Comp.Min, ent.Comp.Max);
        var newVolume = ent.Comp.BaseVolume.Value * factor;

        var bloodLevel = _bloodstream.GetBloodLevel((ent.Owner, bloodstream));
        _bloodstream.SetBloodMaxVolume((ent.Owner, bloodstream), newVolume);
        _bloodstream.TryRegulateBloodLevel((ent.Owner, bloodstream), newVolume * bloodstream.MaxVolumeModifier, bloodLevel);
        // Floof Section End

        return true;
    }
}
