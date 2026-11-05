// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Content.Shared._Floof.Traits;
using Content.Shared._Floof.Traits.Effects;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI.ProfileEditorControls;

public sealed partial class ProfilePreviewSpriteView
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private ILogManager _log = default!;
    private void ApplyTraits(EntityUid player, IReadOnlySet<ProtoId<TraitPrototype>> traits)
    {

        foreach (var traitId in traits)
        {
            if (!_prototypeManager.TryIndex(traitId, out var trait))
                continue;
            
            var transform = EntMan.GetComponent<TransformComponent>(PreviewDummy);

            var effectCtx = new TraitEffectContext
            {
                Player = PreviewDummy,
                EntMan = EntMan,
                Proto = _prototypeManager,
                CompFactory = _factory,
                LogMan = _log,
                Transform = transform,
            };

            foreach (var effect in trait.Effects)
            {
                try
                {
                    effect.Apply(effectCtx);
                }
                catch (Exception e)
                {
                    Log.Error($"Error applying effect {effect.GetType().Name} for trait {trait.ID}: {e}");
                }
            }
        }
    }
}