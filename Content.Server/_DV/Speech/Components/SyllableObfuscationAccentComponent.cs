using Content.Shared._DV.Speech.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._DV.Speech.Components;

[RegisterComponent]
public sealed partial class SyllableObfuscationAccentComponent : Component
{
    [DataField(required: true)]
    public string Accent = default!;
}
