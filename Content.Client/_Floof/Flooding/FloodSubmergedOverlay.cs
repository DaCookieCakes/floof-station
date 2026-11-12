using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Drawn with the world's lighting still bound and before the field of view is cut out,
///     so the tint is lit like a sprite and walls still hide what's behind them.
/// </summary>
public sealed partial class FloodSubmergedOverlay : Overlay
{
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> WaveShader = "FloodSubmerged";
    private static readonly ProtoId<ShaderPrototype> TintShader = "FloodSubmergedTint";

    private readonly ShaderInstance _wave;
    private readonly ShaderInstance _tint;

    public float Strength;
    public Vector3 Tint;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    public override bool RequestScreenTexture => true;

    public FloodSubmergedOverlay()
    {
        IoCManager.InjectDependencies(this);
        _wave = _proto.Index(WaveShader).InstanceUnique();
        _tint = _proto.Index(TintShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0f;
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
