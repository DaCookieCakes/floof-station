using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.MapLayer.Transit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TransitDockPreviewComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 Position;

    [DataField, AutoNetworkedField]
    public Angle Rotation;
}
