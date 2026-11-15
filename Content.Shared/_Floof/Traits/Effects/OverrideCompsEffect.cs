// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Traits.Effects;

/// <summary>
/// Effect that overrides component fields on the player entity.
/// If the component exists, its fields are overwritten with the new values.
/// If it doesn't exist, the component is added.
/// </summary>
public sealed partial class OverrideCompsEffect : BaseTraitEffect
{
    /// <summary>
    /// The components to add/override on the entity.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    public override void Apply(TraitEffectContext ctx)
    {
        ctx.EntMan.AddComponents(ctx.Player, Components, removeExisting: true);
    }
}
