using Robust.Shared.Serialization;

namespace Content.Shared._DEN.Bed.Cryostorage;

/// <summary>
/// Sent from the client to the server when their preference about ignoring cryo messages changes.
/// </summary>
/// <param name="ignore">Whether the client would like to ignore cryo messages.</param>
[Serializable, NetSerializable]
public sealed class IgnoreCryoMessage(bool ignore) : EntityEventArgs
{
    public bool Ignore { get; } = ignore;
}