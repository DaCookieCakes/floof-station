using System.Numerics;
using Content.Client._Floof.MapLayer;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Flooding;

public sealed partial class FloodSubmergedOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> WaveShader = "FloodSubmerged";
    private static readonly ProtoId<ShaderPrototype> TintShader = "FloodSubmergedTint";

    private readonly ShaderInstance _wave;
    private readonly ShaderInstance _tint;
    private readonly MapLayerSystem _layers;

    public float Strength;
    public Vector3 Tint;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public FloodSubmergedOverlay()
    {
        IoCManager.InjectDependencies(this);
        _wave = _proto.Index(WaveShader).InstanceUnique();
        _tint = _proto.Index(TintShader).InstanceUnique();
        _layers = _entManager.System<MapLayerSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0f && !_layers.IsDrawingOtherLayer(args.Viewport);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;

        _wave.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _wave.SetParameter("strength", Strength);
        handle.UseShader(_wave);
        handle.DrawRect(args.WorldBounds, Color.White);

        _tint.SetParameter("strength", Strength);
        _tint.SetParameter("tint", Tint);
        handle.UseShader(_tint);
        handle.DrawRect(args.WorldBounds, Color.White);

        handle.UseShader(null);
    }
}
