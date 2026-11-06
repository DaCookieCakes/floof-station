using Content.Shared._Floof.Traits.Effects;


namespace Content.Client._Floof.Traits.Effects;

public sealed partial class SpawnItemInHandEffect : SharedSpawnItemInHandEffect
{
    public override void Apply(TraitEffectContext ctx)
    {
        Logger.Info("I am the client and the apply trait thingy got called, somehow.");
    }
}