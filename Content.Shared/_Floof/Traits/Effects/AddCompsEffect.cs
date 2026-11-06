// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Traits.Effects;

/// <summary>
/// Effect that adds components to the player entity.
/// Components are added without overwriting existing ones.
/// </summary>
public sealed partial class AddCompsEffect : BaseTraitEffect
{
    /// <summary>
    /// The components to add to the entity.
    /// </summary>
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    public override void Apply(TraitEffectContext ctx)
    {
        ctx.EntMan.AddComponents(ctx.Player, Components, removeExisting: false);
    }
}
