using System.Numerics;
using Content.Client.Overlays;
using Content.Shared._Floof.MapLayer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Floof.MapLayer;

public sealed partial class MapLayerRoofOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> RoofShader = "MapLayerRoof";

    private const int MaxLights = 8;

    private readonly MapLayerDownlightSystem _downlights;
    private readonly MapLayerSystem _layers;
    private readonly MapLayerRoofSystem _roofs;
    private readonly SharedMapSystem _map;
    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;

    private readonly Vector2[] _lightPositions = new Vector2[MaxLights];
    private readonly float[] _lightRadii = new float[MaxLights];
    private readonly Color[] _lightColors = new Color[MaxLights];
    private readonly float[] _lightEnergies = new float[MaxLights];

    private List<Entity<MapGridComponent>> _grids = new();
    private ResPath? _texturePath;
    private Texture? _texture;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MapLayerRoofOverlay()
    {
        IoCManager.InjectDependencies(this);
        _downlights = _entManager.System<MapLayerDownlightSystem>();
        _layers = _entManager.System<MapLayerSystem>();
        _roofs = _entManager.System<MapLayerRoofSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _shader = _proto.Index(RoofShader).InstanceUnique();
        ZIndex = 900;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return TryGetRoof(args, out _, out _, out _);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!TryGetRoof(args, out var map, out var roof, out var alpha))
            return;

        var texture = GetTexture(roof.Texture);
        var color = roof.Color.WithAlpha(roof.Color.A * alpha);
        var size = texture.Size;
        var handle = args.WorldHandle;

        SetLights(args, map);
        handle.UseShader(_shader);

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids, includeMap: false);
        foreach (var grid in _grids)
        {
            if (_entManager.HasComponent<PhantomGridComponent>(grid))
                continue;

            var (_, rotation, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
            var bounds = invMatrix.TransformBox(args.WorldBounds);
            var overhang = MapLayerRoofSystem.GetOverhang(rotation + (args.Viewport.Eye?.Rotation ?? Angle.Zero));
            float tileSize = grid.Comp.TileSize;
            handle.SetTransform(matrix);

            foreach (var piece in _roofs.GetPieces(grid))
            {
                var uv = MapLayerRoofSystem.GetUv(piece, overhang);
                var rect = new Box2((piece.Tile + uv.BottomLeft) * tileSize, (piece.Tile + uv.TopRight) * tileSize);
                if (!rect.Intersects(bounds))
                    continue;

                var region = new UIBox2(uv.Left * size.X, (1f - uv.Top) * size.Y, uv.Right * size.X, (1f - uv.Bottom) * size.Y);
                handle.DrawTextureRectRegion(texture, rect, color, region);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private void SetLights(in OverlayDrawArgs args, EntityUid map)
    {
        var lit = _lightManager.Enabled && args.Viewport.Eye is not { DrawLight: false };
        var ambient = lit ? Color.FromSrgb(_clyde.GetClearColor(map)) : Color.White;
        if (lit && _overlays.TryGetOverlay<NightVisionOverlay>(out var nightVision))
        {
            var boost = nightVision.LightingColor;
            ambient = Color.InterpolateBetween(ambient, Color.FromSrgb(boost).WithAlpha(1f), boost.A);
        }

        _shader.SetParameter("ambient", new Vector4(ambient.R, ambient.G, ambient.B, 1f));

        var count = 0;
        foreach (var light in _downlights.RoofLights)
        {
            if (light.Map != map || !args.WorldAABB.Enlarged(light.Radius).Contains(light.Position))
                continue;

            _lightPositions[count] = light.Position;
            _lightRadii[count] = light.Radius;
            _lightColors[count] = light.Color;
            _lightEnergies[count] = light.Energy;

            if (++count == MaxLights)
                break;
        }

        _shader.SetParameter("light_count", count);
        _shader.SetParameter("light_position", _lightPositions);
        _shader.SetParameter("light_radius", _lightRadii);
        _shader.SetParameter("light_color", _lightColors);
        _shader.SetParameter("light_energy", _lightEnergies);
    }

    private bool TryGetRoof(in OverlayDrawArgs args, out EntityUid map, out MapLayerRoof roof, out float alpha)
    {
        map = _map.GetMapOrInvalid(args.MapId);
        roof = default;
        alpha = 0f;
        if (!_layers.TryGetOtherLayerGap(args.Viewport, out var gap) ||
            gap <= 0f ||
            !map.Valid ||
            !_layers.TryGetRoof(map, out roof))
            return false;

        alpha = roof.GetAlpha(gap);
        return alpha > 0f;
    }

    private Texture GetTexture(ResPath path)
    {
        if (_texture == null || _texturePath != path)
        {
            _texture = _sprite.Frame0(new SpriteSpecifier.Texture(path));
            _texturePath = path;
        }

        return _texture;
    }
}
