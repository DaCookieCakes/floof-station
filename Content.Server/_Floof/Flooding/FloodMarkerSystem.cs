using Content.Server.Atmos.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Floof.Flooding;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using static Content.Shared._Floof.Flooding.FloodConstants;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     Fills the rooms of <see cref="FloodMarkerComponent"/>s.
/// </summary>
public sealed partial class FloodMarkerSystem : EntitySystem
{
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    [Dependency] private EntityQuery<AirtightComponent> _airtightQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;

    private readonly HashSet<Vector2i> _filled = new();
    private readonly Queue<Vector2i> _open = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FloodMarkerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FloodMarkerComponent> ent, ref MapInitEvent args)
    {
        Fill(ent);
        QueueDel(ent);
    }

    private void Fill(Entity<FloodMarkerComponent> marker)
    {
        var xform = Transform(marker);
        if (xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        var volume = TileVolume * marker.Comp.Depth * marker.Comp.Depth;
        var count = 0;
        var leaks = false;

        _filled.Clear();
        _open.Clear();
        var start = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        _filled.Add(start);
        _open.Enqueue(start);

        while (_open.TryDequeue(out var indices))
        {
            if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile))
                continue;

            _puddle.TrySpillAt(tile, new Solution(marker.Comp.Reagent, volume), out _, false, false);
            count++;

            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var dir = (AtmosDirection) (1 << i);
                var neighbor = indices.Offset(dir);

                if (IsBlocked((gridUid, grid), indices, dir) ||
                    IsBlocked((gridUid, grid), neighbor, dir.GetOpposite()))
                    continue;

                if (!_map.TryGetTileRef(gridUid, grid, neighbor, out var next) ||
                    next.Tile.IsEmpty ||
                    _turf.GetContentTileDefinition(next).MapAtmosphere)
                {
                    leaks = true;
                    continue;
                }

                if (count + _open.Count < marker.Comp.MaxTiles && _filled.Add(neighbor))
                    _open.Enqueue(neighbor);
            }
        }

        if (count >= marker.Comp.MaxTiles)
            Log.Warning($"{ToPrettyString(marker)} hit its limit of {marker.Comp.MaxTiles} tiles, the room probably has a gap.");

        if (leaks)
            Log.Warning($"{ToPrettyString(marker)} is in a room open to space, its water will drain away.");
    }

    private bool IsBlocked(Entity<MapGridComponent> grid, Vector2i indices, AtmosDirection dir)
    {
        var anchored = _map.GetAnchoredEntities(grid, grid.Comp, indices);
        while (anchored.MoveNext(out var ent))
        {
            if (_airtightQuery.TryComp(ent, out var airtight) && airtight.AirBlocked && (airtight.AirBlockedDirection & dir) != 0)
                return true;
        }

        return false;
    }
}
