using System.Numerics;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.MapLayer;

public sealed partial class MapLayerRoofSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly HashSet<ProtoId<ContentTileDefinition>> GlassTiles =
    [
        "FloorGlass",
        "FloorRGlass",
        "FloorGlassPlasma",
        "FloorGlassUranium",
        "FloorGlassPlastitanium",
    ];

    private static readonly (Vector2i Side, Box2 Uv)[] Quarters =
    {
        (new Vector2i(-1, -1), new Box2(0f, 0f, 0.5f, 0.5f)),
        (new Vector2i(1, -1), new Box2(0.5f, 0f, 1f, 0.5f)),
        (new Vector2i(-1, 1), new Box2(0f, 0.5f, 0.5f, 1f)),
        (new Vector2i(1, 1), new Box2(0.5f, 0.5f, 1f, 1f)),
    };

    private static readonly Box2 WholeTile = new(0f, 0f, 1f, 1f);

    private const float UpOverhang = 8f;
    private const float DownOverhang = 1f;
    private const float SideOverhang = 4f;

    private readonly HashSet<Vector2i> _tiles = new();
    private readonly HashSet<Vector2i> _glass = new();

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new MapLayerRoofOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<MapLayerRoofOverlay>();
    }

    public IReadOnlyList<RoofPiece> GetPieces(Entity<MapGridComponent> grid)
    {
        var roof = EnsureComp<MapLayerRoofComponent>(grid);
        if (roof.Stale)
        {
            Build(grid, roof.Pieces);
            roof.Stale = false;
        }

        return roof.Pieces;
    }

    [SubscribeLocalEvent]
    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (TryComp<MapLayerRoofComponent>(args.Entity, out var roof))
            roof.Stale = true;
    }

    private void Build(Entity<MapGridComponent> grid, List<RoofPiece> pieces)
    {
        pieces.Clear();
        _tiles.Clear();
        _glass.Clear();

        var tiles = _map.GetAllTilesEnumerator(grid, grid.Comp);
        while (tiles.MoveNext(out var tile))
        {
            if (_turf.IsSpace(tile.Value.Tile))
                continue;

            var indices = tile.Value.GridIndices;
            _tiles.Add(indices);

            if (GlassTiles.Contains(_turf.GetContentTileDefinition(tile.Value).ID))
                _glass.Add(indices);
        }

        foreach (var indices in _tiles)
        {
            if (_glass.Contains(indices))
                continue;

            var corners = 0;
            for (var i = 0; i < Quarters.Length; i++)
            {
                var side = Quarters[i].Side;
                if (_tiles.Contains(indices + new Vector2i(side.X, 0)) &&
                    _tiles.Contains(indices + new Vector2i(0, side.Y)) &&
                    _tiles.Contains(indices + side))
                    corners |= 1 << i;
            }

            if (corners == (1 << Quarters.Length) - 1)
            {
                pieces.Add(new RoofPiece(indices, WholeTile, DirectionFlag.None));
                continue;
            }

            for (var i = 0; i < Quarters.Length; i++)
            {
                if ((corners & (1 << i)) == 0)
                    continue;

                var (side, uv) = Quarters[i];
                var open = DirectionFlag.None;
                if ((corners & (1 << (i ^ 1))) == 0)
                    open |= side.X < 0 ? DirectionFlag.East : DirectionFlag.West;
                if ((corners & (1 << (i ^ 2))) == 0)
                    open |= side.Y < 0 ? DirectionFlag.North : DirectionFlag.South;

                pieces.Add(new RoofPiece(indices, uv, open));
            }
        }
    }

    public static RoofOverhang GetOverhang(Angle screenRotation)
    {
        var angle = screenRotation.RoundToCardinalAngle();
        return new RoofOverhang(
            Reach(angle, new Vector2(0f, 1f)),
            Reach(angle, new Vector2(1f, 0f)),
            Reach(angle, new Vector2(0f, -1f)),
            Reach(angle, new Vector2(-1f, 0f)));
    }

    private static float Reach(Angle angle, Vector2 side)
    {
        var up = angle.RotateVec(side).Y;
        var pixels = up > 0.5f ? UpOverhang : up < -0.5f ? DownOverhang : SideOverhang;
        return pixels / EyeManager.PixelsPerMeter;
    }

    public static Box2 GetUv(RoofPiece piece, RoofOverhang overhang)
    {
        var uv = piece.Uv;
        if ((piece.Open & DirectionFlag.North) != 0)
            uv.Top += overhang.North;
        if ((piece.Open & DirectionFlag.East) != 0)
            uv.Right += overhang.East;
        if ((piece.Open & DirectionFlag.South) != 0)
            uv.Bottom -= overhang.South;
        if ((piece.Open & DirectionFlag.West) != 0)
            uv.Left -= overhang.West;

        return uv;
    }
}
