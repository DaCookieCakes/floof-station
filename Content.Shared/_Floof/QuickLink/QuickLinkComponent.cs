using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.QuickLink;

[RegisterComponent, NetworkedComponent]
public sealed partial class QuickLinkComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier LinkSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    [DataField]
    public SoundSpecifier UnlinkSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");
}
