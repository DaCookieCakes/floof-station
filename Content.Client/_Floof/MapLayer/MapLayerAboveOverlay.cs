using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.MapLayer;

public sealed partial class MapLayerAboveOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> AboveShader = "MapLayerAbove";

    private readonly MapLayerSystem _layers;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MapLayerAboveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _layers = _entManager.System<MapLayerSystem>();
        _shader = _proto.Index(AboveShader).InstanceUnique();
        ZIndex = 1000;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _layers.TryGetDrawnAbove(args.Viewport, out _, out _);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_layers.TryGetDrawnAbove(args.Viewport, out var above, out var alpha))
            return;

        _shader.SetParameter("above", above);
        _shader.SetParameter("viewport_size", (Vector2) args.Viewport.Size);
        _shader.SetParameter("alpha", alpha);

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
