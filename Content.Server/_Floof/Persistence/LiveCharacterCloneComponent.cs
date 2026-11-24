namespace Content.Server._Floof.Persistence;

/// <summary>
///     A clone of a character body. Takes over the character once the character's own player enters it.
/// </summary>
[RegisterComponent]
public sealed partial class LiveCharacterCloneComponent : Component
{
    [ViewVariables]
    public EntityUid Source;
}
