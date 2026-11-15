using Content.Shared.Silicons.Laws;
using Robust.Shared.Prototypes;

namespace Content.Shared._Floof.Silicons.Borgs;

/// <summary>
/// Replaces a borg's laws when added. Meant for a borg type's <c>addComponents</c>, so picking the type
/// (e.g. the K9 security dogborg) gives it a dedicated lawset.
/// </summary>
[RegisterComponent]
public sealed partial class BorgTypeLawsComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SiliconLawsetPrototype> Laws;
}
