// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Content.Client._Floof.Traits.UI;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Traits;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Traits;

/// <summary>
/// Client system that shows a popup when traits are disabled due to unmet conditions.
/// </summary>
public sealed partial class DisabledTraitsPopupSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private DisabledTraitsPopup? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DisabledTraitsEvent>(OnDisabledTraits);
    }

    private void OnDisabledTraits(DisabledTraitsEvent ev)
    {
        // Don't show if user has opted to skip this popup
        if (_cfg.GetCVar(CCVars.SkipDisabledTraitsPopup))
            return;

        // Don't show if no traits were actually disabled
        if (ev.DisabledTraits.Count == 0)
            return;

        OpenDisabledTraitsPopup(ev.DisabledTraits);
    }

    private void OpenDisabledTraitsPopup(Dictionary<ProtoId<TraitPrototype>, List<string>> disabledTraits)
    {
        // Close existing window if one is open
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }

        _window = new DisabledTraitsPopup(disabledTraits);
        _window.OpenCentered();
        _window.OnClose += () => _window = null;
    }
}
