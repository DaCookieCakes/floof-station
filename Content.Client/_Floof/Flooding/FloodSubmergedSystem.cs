using System.Numerics;
using Content.Client.Fluids;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Swimming;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player's head under, the view takes on a deep shade of the liquid and wavers,
///     but the liquid itself turns clear, so diving is how you find what's down there.
/// </summary>
public sealed partial class FloodSubmergedSystem : EntitySystem
{
    [Dependency] private FloodLevelSystem _level = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SwimmingSystem _swimming = default!;

    private const float FadeSeconds = 0.3f;
    private const float Clarity = 0.35f;
    private const float HueSeconds = 0.5f;

    /// <summary>
    ///     What lies above the water looks like from below it, when the local player is under.
    /// </summary>
    private const float AboveDarkness = 0.8f;
    private const float TintBrightness = 0.16f;
    private const float TintSaturation = 3f;
    private static readonly Color WaterHue = new(0.46f, 0.73f, 1f);

    private FloodSubmergedOverlay _view = default!;
    private bool _submerged;

    /// <summary>
    ///     How far the local player's view has turned to underwater, 0-1.
    /// </summary>
    public float SubmergedView { get; private set; }

    /// <summary>
    ///     The colour of the liquid the local player is under, at full brightness.
    /// </summary>
    public Color SubmergedHue { get; private set; } = WaterHue;

    /// <summary>
    ///     A deep, saturated version of the liquid. Water comes out dark blue, blood dark red.
    /// </summary>
    public Vector3 SubmergedTint => new(
        MathF.Pow(SubmergedHue.R, TintSaturation) * TintBrightness,
        MathF.Pow(SubmergedHue.G, TintSaturation) * TintBrightness,
        MathF.Pow(SubmergedHue.B, TintSaturation) * TintBrightness);

    public Vector3 CausticsColor => new(MathF.Sqrt(SubmergedHue.R), MathF.Sqrt(SubmergedHue.G), MathF.Sqrt(SubmergedHue.B));

    public Color AboveColor
    {
        get
        {
            var tint = SubmergedTint;
            return new Color(tint.X, tint.Y, tint.Z, AboveDarkness * SubmergedView);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        _view = new FloodSubmergedOverlay();
        _overlay.AddOverlay(_view);
        _overlay.AddOverlay(new FloodCausticsOverlay());
        _overlay.AddOverlay(new FloodFogOverlay());
        _overlay.AddOverlay(new FloodDrySurfaceOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay(_view);
        _overlay.RemoveOverlay<FloodCausticsOverlay>();
        _overlay.RemoveOverlay<FloodFogOverlay>();
        _overlay.RemoveOverlay<FloodDrySurfaceOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var submerged = false;
        if (_player.LocalEntity is { } uid && TryComp(uid, out TransformComponent? xform) &&
            HasComp<SwimmerComponent>(uid) && _swimming.IsSubmerged(uid, xform))
        {
            submerged = true;
            if (_level.TryGetLiquidColor(xform, out var liquid))
                UpdateHue(liquid, frameTime);
        }

        _view.Strength = Math.Clamp(_view.Strength + (submerged ? frameTime : -frameTime) / FadeSeconds, 0f, 1f);
        _view.Tint = SubmergedTint;
        SubmergedView = _view.Strength;

        if (submerged == _submerged)
            return;

        _submerged = submerged;
        _puddle.SetFloodClarity(submerged ? Clarity : 1f);
    }

    private void UpdateHue(Color liquid, float frameTime)
    {
        var brightest = MathF.Max(liquid.R, MathF.Max(liquid.G, liquid.B));
        if (brightest <= 0f)
            return;

        var hue = new Color(liquid.R / brightest, liquid.G / brightest, liquid.B / brightest);

        // Diving in shows the new liquid straight away, swimming into another one blends over.
        var blend = _view.Strength <= 0f ? 1f : 1f - MathF.Exp(-frameTime / HueSeconds);
        SubmergedHue = Color.InterpolateBetween(SubmergedHue, hue, blend);
    }
}
