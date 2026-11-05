using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Ghost;

/// <summary>
///     Lets a ghost go back to the lobby once <see cref="RespawnTime"/> has passed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GhostRespawnComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public TimeSpan? RespawnTime;
}

[Serializable, NetSerializable]
public sealed class GhostRespawnRequestEvent : EntityEventArgs;
