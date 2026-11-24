using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Traits.Effects;

/// <summary>
/// Shared class inherited by both client and server
/// </summary>
public abstract partial class SharedSpawnItemInHandEffect : BaseTraitEffect
{
    /// <summary>
    /// The entity prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Item = string.Empty;
}
