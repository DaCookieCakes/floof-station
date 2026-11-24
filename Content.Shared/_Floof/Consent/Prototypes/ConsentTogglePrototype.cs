using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Consent.Prototypes;

/// <summary>
///     An opt-in consent toggle, set per character in the character editor.
/// </summary>
[Prototype]
public sealed partial class ConsentTogglePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name { get; private set; } = string.Empty;

    [DataField]
    public LocId Description { get; private set; } = string.Empty;
}
