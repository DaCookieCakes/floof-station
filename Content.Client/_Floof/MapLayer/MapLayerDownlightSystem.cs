using System.Numerics;
using Content.Shared._Floof.MapLayer;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Floof.MapLayer;

public sealed partial class MapLayerDownlightSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly List<RoofLight> _roofLights = new();
    private readonly Dictionary<(EntityUid Source, EntityUid Map), EntityUid> _phantoms = new();
    private readonly HashSet<(EntityUid Source, EntityUid Map)> _usedPhantoms = new();
    private readonly List<(EntityUid Source, EntityUid Map)> _unusedPhantoms = new();
    private readonly List<(EntityUid Map, float Gap)> _gaps = new();
    private List<Entity<MapGridComponent>> _grids = new();

    public IReadOnlyList<RoofLight> RoofLights => _roofLights;

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var phantom in _phantoms.Values)
        {
            QueueDel(phantom);
        }

        _phantoms.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _roofLights.Clear();
        _usedPhantoms.Clear();

        var eyeMap = _map.GetMapOrInvalid(_eye.CurrentEye.Position.MapId);
        var query = EntityQueryEnumerator<MapLayerDownlightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var downlight, out var xform))
        {
            if (xform.MapUid is not { } ownMap ||
                !_lights.TryGetLight(uid, out var light) ||
                !light.Enabled ||
                !_layers.TryGetGaps(ownMap, _gaps))
                continue;

            var (origin, rotation) = _xform.GetWorldPositionRotation(xform);
            var lean = rotation.ToWorldVec() * downlight.Tilt;

            for (var i = _gaps.Count - 1; i >= 0; i--)
            {
                var (map, gap) = _gaps[i];
                if (gap <= 0f)
                    continue;

                if (gap >= downlight.Reach)
                    break;

                var radius = downlight.Radius + downlight.Spread * gap;
                var energy = downlight.Energy * (1f - gap / downlight.Reach);
                var position = origin + lean * gap;

                _roofLights.Add(new RoofLight(map, position, radius, light.Color, energy));

                var blocked = IsBlocked(map, position);
                if (!blocked || map != eyeMap)
                    UpdatePhantom((uid, map), position, light, radius, energy);

                if (blocked)
                    break;
            }
        }

        RemoveUnusedPhantoms();
    }

    private bool IsBlocked(EntityUid map, Vector2 position)
    {
        _grids.Clear();
        _map.FindGridsIntersecting(Transform(map).MapID,
            Box2.CenteredAround(position, new Vector2(0.01f)),
            ref _grids,
            includeMap: false);

        foreach (var grid in _grids)
        {
            if (HasComp<PhantomGridComponent>(grid))
                continue;

            if (_map.TryGetTileRef(grid, grid.Comp, position, out var tile) &&
                !tile.Tile.IsEmpty &&
                !_turf.IsSpace(tile.Tile))
                return true;
        }

        return false;
    }

    private void UpdatePhantom((EntityUid Source, EntityUid Map) key,
        Vector2 position,
        SharedPointLightComponent light,
        float radius,
        float energy)
    {
        _usedPhantoms.Add(key);

        if (!_phantoms.TryGetValue(key, out var phantom) || TerminatingOrDeleted(phantom))
        {
            phantom = Spawn(null, new MapCoordinates(position, Transform(key.Map).MapID));
            _phantoms[key] = phantom;
        }
        else
        {
            _xform.SetWorldPosition(phantom, position);
        }

        var phantomLight = _lights.EnsureLight(phantom);
        _lights.SetCastShadows(phantom, false, phantomLight);
        _lights.SetColor(phantom, light.Color, phantomLight);
        _lights.SetRadius(phantom, radius, phantomLight);
        _lights.SetEnergy(phantom, energy, phantomLight);
        _lights.SetSoftness(phantom, light.Softness, phantomLight);
        _lights.SetFalloff(phantom, light.Falloff, phantomLight);
        _lights.SetCurveFactor(phantom, light.CurveFactor, phantomLight);
    }

    private void RemoveUnusedPhantoms()
    {
        _unusedPhantoms.Clear();
        foreach (var key in _phantoms.Keys)
        {
            if (!_usedPhantoms.Contains(key))
                _unusedPhantoms.Add(key);
        }

        foreach (var key in _unusedPhantoms)
        {
            QueueDel(_phantoms[key]);
            _phantoms.Remove(key);
        }
    }
}

public readonly record struct RoofLight(EntityUid Map, Vector2 Position, float Radius, Color Color, float Energy);
