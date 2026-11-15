namespace Content.Shared._Floof.Link;

/// <summary>
/// Links entities across different grids and maps on mapinit.
/// </summary>
[RegisterComponent]
public sealed partial class LinkKeyComponent : Component
{
    [DataField(required: true)]
    public string Key = string.Empty;
}
