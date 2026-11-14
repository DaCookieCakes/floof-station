namespace Content.Server._Floof.Link;

[RegisterComponent, Access(typeof(GasLinkSystem))]
public sealed partial class GasLinkComponent : Component
{
    /// Gas only flows while both ends are enabled.
    [DataField]
    public bool Enabled = true;
}
