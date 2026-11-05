// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Floof.HeightAdjust;
using Content.Shared.Sprite;
using Robust.Shared.Configuration;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._EE.HeightAdjust;

public sealed partial class HeightAdjustSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedScaleVisualsSystem _scaleVisuals = default!; // Floof - HeightWidth
    [Dependency] private IConfigurationManager _config = default!;

    /// <summary>
    ///     Changes the radius of fixtures based on a provided float scale
    /// </summary>
    /// <param name="uid">The entity to modify values for</param>
    /// <param name="scale">The scale to multiply values by</param>
    /// <returns>True if all operations succeeded</returns>
    public bool SetScale(EntityUid uid, float scale)
    {
        return SetScale(uid, new Vector2(scale, scale));
    }

    /// <summary>
    ///     Changes the radius of fixtures based on a provided Vector2 scale
    /// </summary>
    /// <param name="uid">The entity to modify values for</param>
    /// <param name="scale">The scale to multiply values by</param>
    /// <returns>True if all operations succeeded</returns>
    public bool SetScale(EntityUid uid, Vector2 scale)
    {
        var succeeded = true;
        var avg = (scale.X + scale.Y) / 2;

        if (_config.GetCVar(CCVar.CCVars.HeightAdjustModifiesHitbox) && TryComp<FixturesComponent>(uid, out var fixtures)) // Floof - HeightWidth
        {
            // Floof Section - HeightWidth
            var adjusted = EnsureComp<HeightAdjustedFixturesComponent>(uid);
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                adjusted.BaseRadii.TryAdd(id, fixture.Shape.Radius);
                _physics.SetRadius(uid, id, fixture, fixture.Shape, MathF.MinMagnitude(adjusted.BaseRadii[id] * avg, 0.49f));
            }
            // Floof Section End
        }
        else
            succeeded = false;

        _scaleVisuals.SetSpriteScale(uid, scale); // Floof - HeightWidth

        RaiseLocalEvent(uid, new HeightAdjustedEvent { NewScale = scale });

        return succeeded;
    }
}
