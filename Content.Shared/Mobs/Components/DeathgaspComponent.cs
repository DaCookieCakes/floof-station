using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Mobs;

/// <summary>
///     Mobs with this component will emote a deathgasp when they die.
/// </summary>
/// <see cref="DeathgaspSystem"/>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DeathgaspComponent : Component
{
    /// <summary>
    ///     The emote prototype to use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> Prototype = "DefaultDeathgasp";

    /// <summary>
    /// Starlight - The damage that is taken when succumbing
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Asphyxiation";
}
