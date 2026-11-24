namespace Content.Client._Floof.MapLayer;

[RegisterComponent]
public sealed partial class MapLayerRoofComponent : Component
{
    [ViewVariables]
    public bool Stale = true;

    [ViewVariables]
    public readonly List<RoofPiece> Pieces = new();
}

public readonly record struct RoofPiece(Vector2i Tile, Box2 Uv, DirectionFlag Open);

public readonly record struct RoofOverhang(float North, float East, float South, float West);
