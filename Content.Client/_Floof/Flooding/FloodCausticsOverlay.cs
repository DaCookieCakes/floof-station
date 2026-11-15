using System.Numerics;
using Content.Shared._Floof.Flooding;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player under, wet tiles ripple and carry the moving web of light seen on the floor of a sunlit pool.
///     Deeper water shows more of it. Drawn before the screen tint, so that tints it too.
/// </summary>
public sealed partial class FloodCausticsOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodCaustics";

    private readonly FloodWaterlineSystem _system;
    private readonly FloodSubmergedSystem _submerged;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public FloodCausticsOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWaterlineSystem>();
        _submerged = _entManager.System<FloodSubmergedSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();

        ZIndex = -2;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _submerged.SubmergedView > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye is not { } eye)
            return;

        var handle = args.WorldHandle;
        var origin = args.Viewport.WorldToLocal(Vector2.Zero);
        var scale = (args.Viewport.WorldToLocal(eye.Position.Position + Vector2.UnitX) -
                     args.Viewport.WorldToLocal(eye.Position.Position)).Length();

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("world_origin", new Vector2(origin.X, args.Viewport.Size.Y - origin.Y));
        _shader.SetParameter("tile_pixels", scale);
        _shader.SetParameter("light_color", _submerged.CausticsColor);
        handle.UseShader(_shader);

        foreach (var (grid, tiles) in _system.Grids)
        {
            if (!_entManager.HasComponent<MapGridComponent>(grid))
                continue;

            var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
            handle.SetTransform(matrix);
            var bounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(1f);

            foreach (var (indices, tile) in tiles)
            {
                if (tile.Depth <= 0f || !bounds.Contains(indices))
                    continue;

                var box = new Box2(indices, indices + Vector2i.One);
                handle.DrawRect(box, Color.White.WithAlpha(tile.Depth * _submerged.SubmergedView));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }
}
