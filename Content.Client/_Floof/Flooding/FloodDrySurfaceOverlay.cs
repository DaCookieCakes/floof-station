using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player under, shows the flood's shape on the floor: the deeper a tile's water the clearer it is,
///     and everything dry, space included, is darkest. Wet tiles mark the stencil, so one rectangle can cover the rest.
/// </summary>
public sealed partial class FloodDrySurfaceOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodFill";

    private readonly FloodWaterlineSystem _system;
    private readonly FloodSubmergedSystem _submerged;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public FloodDrySurfaceOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWaterlineSystem>();
        _submerged = _entManager.System<FloodSubmergedSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();

        ZIndex = (int) DrawDepth.Puddles + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_submerged.SubmergedView <= 0f)
            return;

        var handle = args.WorldHandle;
        var above = _submerged.AboveColor;

        _shader.Stencil = new StencilParameters
        {
            Enabled = true,
            Ref = _system.NextStencilRef(),
            Op = StencilOp.Replace,
            Func = StencilFunc.NotEqual,
        };
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
                handle.DrawRect(box, above.WithAlpha(above.A * (1f - tile.Depth)));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.DrawRect(args.WorldBounds, above);

        handle.UseShader(null);
    }
}
