// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Speech.Components;
using Robust.Shared.Random;
using Content.Shared.Speech; // Floof - API
using Content.Shared.Speech.EntitySystems; // Floof - API

namespace Content.Server.Speech.EntitySystems;

public sealed partial class CheeseAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CheeseAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, CheeseAccentComponent component, ref AccentGetEvent args) // Floof - API
    {
        var message = args.Message;

        message = _replacement.ApplyReplacements(message, "cheese"); // Floof - Goob #3533 id fix

        // Sanitize capital again, in case we substituted a word that should be capitalized
        message = message[0].ToString().ToUpper() + message.Remove(0, 1);

        args.Message = message;
    }
};
