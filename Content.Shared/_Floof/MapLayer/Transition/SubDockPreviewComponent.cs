using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.MapLayer.Transition;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SubDockPreviewComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 Position;

    [DataField, AutoNetworkedField]
    public Angle Rotation;
}
