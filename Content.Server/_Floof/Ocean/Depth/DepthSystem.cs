using Content.Shared.GameTicking;
using Content.Shared._Floof.Ocean.Depth;

namespace Content.Server._Floof.Ocean.Depth;

public sealed partial class DepthSystem : SharedDepthSystem
{
    [Dependency] private GameTicker _ticker = default!;

    [SubscribeLocalEvent]
    private void OnVariationStartup(Entity<DepthVariationComponent> ent, ref ComponentStartup args)
    {
        SetSeed(ent, _ticker.RoundId);
    }
}
