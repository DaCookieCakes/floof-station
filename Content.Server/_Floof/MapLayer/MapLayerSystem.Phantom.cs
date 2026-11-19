using Content.Server.Shuttles.Systems;
using Content.Shared._Floof.MapLayer;
using Content.Shared.Light.Components;
using Content.Shared.Shuttles.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Floof.MapLayer;

public sealed partial class MapLayerSystem
{
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    private const string PhantomTile = "FloorPhantom";

    private readonly List<(Vector2i, Tile)> _phantomTiles = new();
    private List<Entity<MapGridComponent>> _phantomSources = new();

    [PublicAPI]
    public void AddPhantoms(EntityUid layer, EntityUid source, Box2Rotated area)
    {
        _phantomSources.Clear();
        _map.FindGridsIntersecting(source, area, ref _phantomSources, includeMap: false);

        var tile = new Tile(_tileDefs[PhantomTile].TileId);
        foreach (var (grid, gridComp) in _phantomSources)
        {
            _phantomTiles.Clear();
            var tiles = _map.GetTilesIntersecting(grid, gridComp, area);
            while (tiles.MoveNext(out var tileRef))
            {
                _phantomTiles.Add((tileRef.GridIndices, tile));
            }

            if (_phantomTiles.Count == 0)
                continue;

            var phantom = _map.CreateGridEntity(layer);
            EnsureComp<PhantomGridComponent>(phantom);
            _metaData.SetEntityName(phantom, Name(grid));

            RemComp<ShuttleComponent>(phantom);
            RemComp<ImplicitRoofComponent>(phantom);
            _shuttle.AddIFFFlag(phantom, IFFFlags.Hide);

            var (position, rotation) = _xform.GetWorldPositionRotation(grid);
            _xform.SetWorldPositionRotation(phantom, position, rotation);
            _map.SetTiles(phantom, _phantomTiles);
        }
    }
}
