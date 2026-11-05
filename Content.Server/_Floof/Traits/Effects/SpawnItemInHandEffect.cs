using System.Numerics;
using Content.Server.Hands.Systems;
using Content.Shared._Floof.Traits.Effects;
using Content.Shared.Hands.Components;
using Robust.Shared.Map;
using Serilog;

namespace Content.Server._Floof.Traits.Effects;

public sealed partial class SpawnItemInHandEffect : SharedSpawnItemInHandEffect
{   
    public override void Apply(TraitEffectContext ctx)
    {
        var transform = ctx.Transform;
        var entMan = ctx.EntMan;
        var player = ctx.Player;
        var _hands = entMan.System<HandsSystem>();
        var _transform = entMan.System<SharedTransformSystem>();
        if (!entMan.TryGetComponent<HandsComponent>(player, out var hands))
        {
            Log.Warning("Cannot spawn trait item: player has no hands component");
            return;
        }
        
        var item = entMan.Spawn(Item.Id, _transform.GetMapCoordinates(transform));

        if (!_hands.TryPickup(player, item, checkActionBlocker: false, handsComp: hands))
            Log.Debug($"Could not pick up trait item {Item}, leaving at feet");
    }
}