using System.Numerics;
using Content.Client._Floof.AnimatedTiles;
using Content.Shared._Floof.Ocean;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Ocean;

/// <summary>
/// On a map with <see cref="OceanDepthComponent"/>, everything that isn't solid floor is deep water: space, lattice,
/// glass and whatever's past the grid.
/// </summary>
public sealed partial class OceanDeepOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const int MinStencilRef = 2;
    private const int MaxStencilRef = 254;
    private static readonly ProtoId<ShaderPrototype> MaskShader = "AnimatedTile";
    private static readonly ProtoId<ShaderPrototype> DeepShader = "OceanDeep";
    private static readonly ResPath FloorTexture = new("/Textures/Tiles/Asteroid/asteroid.png");
    private static readonly ResPath SurfaceTexture = new("/Textures/_Floof/Tiles/ocean_surface.rsi/water.png");

    // Keeps time with the ocean's animated tile, so the ripples carry on across the move between maps.
    private const int SurfaceFrames = 4;
    private const float SurfaceFrameTime = 0.35f;

    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly TurfSystem _turf;
    private readonly AnimatedTileSystem _animated;
    private readonly ShaderInstance _mask;
    private readonly ShaderInstance _redraw;
    private readonly ShaderInstance _deep;
    private List<Entity<MapGridComponent>> _grids = new();
    private int _stencilRef = MinStencilRef;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public OceanDeepOverlay()
    {
        IoCManager.InjectDependencies(this);
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _turf = _entManager.System<TurfSystem>();
        _animated = _entManager.System<AnimatedTileSystem>();
        _mask = _proto.Index(MaskShader).InstanceUnique();
        _redraw = _proto.Index(MaskShader).InstanceUnique();
        _deep = _proto.Index(DeepShader).InstanceUnique();

        ZIndex = (int) DrawDepth.Puddles + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _entManager.HasComponent<OceanDepthComponent>(_map.GetMapOrInvalid(args.MapId));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entManager.TryGetComponent<OceanDepthComponent>(_map.GetMapOrInvalid(args.MapId), out var depth) ||
            args.Viewport.Eye == null)
            return;

        var now = _timing.CurTime;
        // Where the world's axes land in FRAGCOORD (y up), inverted so the shader can go from pixels to tiles.
        var origin = args.Viewport.WorldToLocal(Vector2.Zero);
        var axisX = args.Viewport.WorldToLocal(Vector2.UnitX) - origin;
        var axisY = args.Viewport.WorldToLocal(Vector2.UnitY) - origin;
        var a = new Vector2(axisX.X, -axisX.Y);
        var b = new Vector2(axisY.X, -axisY.Y);
        var det = a.X * b.Y - b.X * a.Y;
        _deep.SetParameter("world_origin", new Vector2(origin.X, args.Viewport.Size.Y - origin.Y));
        _deep.SetParameter("world_x", new Vector2(b.Y, -b.X) / det);
        _deep.SetParameter("world_y", new Vector2(-a.Y, a.X) / det);
        _deep.SetParameter("depth", depth.GetDepth(now));
        _deep.SetParameter("brightness", depth.GetBrightness(now));
        _deep.SetParameter("eye_position", args.Viewport.Eye.Position.Position);
        _deep.SetParameter("view_center", (Vector2) args.Viewport.Size / 2f);
        _deep.SetParameter("floor_texture", _cache.GetResource<TextureResource>(FloorTexture).Texture);
        _deep.SetParameter("submerged", depth.GetSubmerged(now));
        // Not as a Color, which would get converted while the map's light doesn't.
        _deep.SetParameter("surface_light", new Vector3(depth.SurfaceLight.R, depth.SurfaceLight.G, depth.SurfaceLight.B));
        _deep.SetParameter("surface_frame", (float) ((int) (_timing.RealTime.TotalSeconds / SurfaceFrameTime) % SurfaceFrames));
        _deep.SetParameter("surface_texture", _cache.GetResource<TextureResource>(SurfaceTexture).Texture);

        // A new ref each draw, so marks left in the stencil from earlier frames don't count.
        _stencilRef = _stencilRef >= MaxStencilRef ? MinStencilRef : _stencilRef + 1;
        var stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _stencilRef,
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
        _mask.Stencil = stencil;
        _deep.Stencil = stencil;
        _redraw.Stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _stencilRef,
            Op = StencilOp.Keep,
            Func = StencilFunc.Equal,
        };

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        // Solid floors mark the stencil, so one rectangle of water covers the rest.
        var handle = args.WorldHandle;
        var seeThrough = _animated.SeeThrough;
        handle.UseShader(_mask);
        foreach (var grid in _grids)
        {
            handle.SetTransform(_xform.GetWorldMatrix(grid));
            foreach (var tile in _map.GetTilesIntersecting(grid, grid, args.WorldAABB))
            {
                if (!_turf.IsSpace(tile) && seeThrough[tile.Tile.TypeId] == null)
                    handle.DrawRect(new Box2(tile.GridIndices, tile.GridIndices + Vector2i.One), Color.Transparent);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_deep);
        handle.DrawRect(args.WorldBounds, Color.White);

        DrawSeeThrough(args);

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    /// See-through tiles (lattice, glass) let the water show through, but it covers them too. This draws them again
    /// on top.
    /// </summary>
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

                // Variants sit side by side in the sprite, each as wide as it is tall.
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
