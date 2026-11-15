using System.Numerics;
using StretchMode = Robust.Client.UserInterface.Controls.SpriteView.StretchMode;

namespace Content.Client.Lobby.UI;

// Floof - Preview size
public sealed partial class HumanoidProfileEditor
{
    // Fixed room for a sprite up to 64px wide and 32px tall. Large markings only widen the visible sprite,
    // their padding may overflow vertically instead of resizing the layout.
    private static readonly Vector2 PreviewSpriteSize = new(64, 32);

    private void PreviewSizeInit()
    {
        SpriteView.SetSize = SpriteView.Scale * PreviewSpriteSize;
        SpriteView.Stretch = StretchMode.None;
    }
}
