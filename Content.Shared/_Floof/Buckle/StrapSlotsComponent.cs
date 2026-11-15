using System.Numerics;

namespace Content.Shared._Floof.Buckle;

[RegisterComponent]
public sealed partial class StrapSlotsComponent : Component
{
    [DataField(required: true)]
    public List<Vector2> Offsets = new();
}
