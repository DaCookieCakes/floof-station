using System.Numerics;
using Content.Client._Floof.AnimatedTiles;
using Content.Client._Floof.Graphics;
using Content.Client._Floof.MapLayer;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.Ocean;
using Content.Shared._Floof.Ocean.Depth;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Ocean;

public sealed partial class OceanBackdropOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private static readonly ProtoId<ShaderPrototype> MaskShader = "AnimatedTile";
    private static readonly ProtoId<ShaderPrototype> BackdropShader = "OceanBackdrop";
    private static readonly ProtoId<ShaderPrototype> DeepShader = "OceanDeep";
    private static readonly ResPath SurfaceTexture = new("/Textures/_Floof/Tiles/ocean_surface.rsi/water.png");

    private readonly SharedMapSystem _map;
    private readonly MapLayerSystem _layers;
    private readonly SharedDepthSystem _depth;
    private readonly SharedTransformSystem _xform;
    private readonly TurfSystem _turf;
    private readonly AnimatedTileSystem _animated;
    private readonly StencilRefSystem _stencil;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _redraw;
    private readonly ShaderInstance _backdrop;
    private readonly ShaderInstance _deep;
    private readonly ShaderInstance _haze;
    private bool _dive;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(EntityUid Map, float Z)> _stackLayers = new();

    private const float HazeCover = 0.3f;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public OceanBackdropOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entManager.System<SharedMapSystem>();
        _layers = _entManager.System<MapLayerSystem>();
        _depth = _entManager.System<SharedDepthSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _turf = _entManager.System<TurfSystem>();
        _animated = _entManager.System<AnimatedTileSystem>();
        _stencil = _entManager.System<StencilRefSystem>();
        _mask = _proto.Index(MaskShader).InstanceUnique();
        _redraw = _proto.Index(MaskShader).InstanceUnique();
        _backdrop = _proto.Index(BackdropShader).InstanceUnique();
        _deep = _proto.Index(DeepShader).InstanceUnique();
        _haze = _proto.Index(DeepShader).InstanceUnique();

        ZIndex = (int) DrawDepth.Puddles + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var map = _map.GetMapOrInvalid(args.MapId);
        if (!_entManager.HasComponent<MapFluidComponent>(map) ||
            !_layers.TryGetStack(map, out var stack))
            return false;

        _dive = IsDiveTransition(map, stack);
        if (_dive)
            return !_layers.IsDrawingOtherLayer(args.Viewport);

        return !_layers.TryGetOtherLayerGap(args.Viewport, out var gap) || gap > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var map = _map.GetMapOrInvalid(args.MapId);
        if (args.Viewport.Eye == null ||
            !_layers.TryGetBelowTop(map, out var belowTop))
            return;
        
        var origin = args.Viewport.WorldToLocal(Vector2.Zero);
        var axisX = args.Viewport.WorldToLocal(Vector2.UnitX) - origin;
        var axisY = args.Viewport.WorldToLocal(Vector2.UnitY) - origin;
        var a = new Vector2(axisX.X, -axisX.Y);
        var b = new Vector2(axisY.X, -axisY.Y);
        var det = a.X * b.Y - b.X * a.Y;
        var worldOrigin = new Vector2(origin.X, args.Viewport.Size.Y - origin.Y);
        var worldX = new Vector2(b.Y, -b.X) / det;
        var worldY = new Vector2(-a.Y, a.X) / det;
        var blipDepth = _depth.GetDepth(args.Viewport.Eye.Position) ?? 0f;
        var water = _dive ? _backdrop : _deep;
        SetWater(water, args.Viewport.Eye, worldOrigin, worldX, worldY, belowTop, blipDepth);

        if (_dive)
        {
            var mapLight = _entManager.TryGetComponent<MapLightComponent>(map, out var light)
                ? light.AmbientLightColor
                : Color.Black;
            
            _backdrop.SetParameter("map_light", new Vector3(mapLight.R, mapLight.G, mapLight.B));
        }
        else
        {
            SetWater(_haze, args.Viewport.Eye, worldOrigin, worldX, worldY, belowTop, blipDepth);
            _deep.SetParameter("cover", _layers.GetCover(args.Viewport));
            _haze.SetParameter("cover", HazeCover);
            _deep.SetParameter("lit_cover", 1f);
            _haze.SetParameter("lit_cover", 0f);
        }

        var stencilRef = _stencil.Next();
        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = stencilRef,
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        _mask.Stencil = stencil;
        water.Stencil = stencil;
        _haze.Stencil = stencil;
        _redraw.Stencil = new StencilParameters
        {
            Enabled = true,
            Ref = stencilRef,
            Op = StencilOp.Keep,
            Func = StencilFunc.Equal,
        };

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        var handle = args.WorldHandle;
        var seeThrough = _animated.SeeThrough;
        handle.UseShader(_mask);
        foreach (var grid in _grids)
        {
            if (!_dive && grid.Owner == map)
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (!_turf.IsSpace(tile) && seeThrough[tile.Tile.TypeId] == null)
                    handle.DrawRect(new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One), Color.Transparent);
            }
        }

        if (!_dive)
            DrawHaze(args, map);

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(water);
        handle.DrawRect(args.WorldBounds, Color.White);

        DrawSeeThrough(args);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void SetWater(ShaderInstance shader,
        IEye eye,
        Vector2 worldOrigin,
        Vector2 worldX,
        Vector2 worldY,
        float belowTop,
        float blipDepth)
    {
        shader.SetParameter("world_origin", worldOrigin);
        shader.SetParameter("world_x", worldX);
        shader.SetParameter("world_y", worldY);
        shader.SetParameter("depth", belowTop);
        shader.SetParameter("blip_depth", blipDepth);
        shader.SetParameter("eye_position", eye.Position.Position);
        shader.SetParameter("surface_texture", _cache.GetResource<TextureResource>(SurfaceTexture).Texture);
    }

    private void DrawHaze(in OverlayDrawArgs args, EntityUid map)
    {
        var handle = args.WorldHandle;
        handle.UseShader(_haze);
        foreach (var grid in _grids)
        {
            if (grid.Owner != map)
                continue;

            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (!_turf.IsSpace(tile))
                    handle.DrawRect(new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One), Color.White);
            }
        }
    }

    private bool IsDiveTransition(EntityUid map, EntityUid stack)
    {
        if (_entManager.HasComponent<OceanDiveTransitionComponent>(map))
            return true;

        if (!_entManager.HasComponent<MapLayerTransitionComponent>(map) ||
            !_layers.TryGetZ(map, out var z))
            return false;

        _layers.GetLayers(stack, _stackLayers, true);
        EntityUid? above = null;
        EntityUid? below = null;
        foreach (var (other, otherZ) in _stackLayers)
        {
            if (_entManager.HasComponent<MapLayerTransitionComponent>(other))
                continue;

            if (otherZ <= z)
            {
                below = other;
                continue;
            }

            above = other;
            break;
        }

        return _entManager.HasComponent<OceanDiveTransitionComponent>(above) ||
               _entManager.HasComponent<OceanDiveTransitionComponent>(below);
    }

    private void DrawSeeThrough(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var seeThrough = _animated.SeeThrough;
        handle.UseShader(_redraw);

        foreach (var grid in _grids)
        {
            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (seeThrough[tile.Tile.TypeId] is not { } texture)
                    continue;
                
                var tileSize = texture.Height;
                var left = tile.Tile.Variant * tileSize;
                handle.DrawTextureRectRegion(texture,
                    new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One),
                    null,
                    new UIBox2(left, 0, left + tileSize, tileSize));
            }
        }
    }
}
