using Content.Client._Floof.Flooding;
using Content.Client.IconSmoothing;
using Content.Shared.Chemistry.Components;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using static Content.Shared._Floof.Flooding.FloodConstants;

namespace Content.Client.Fluids;

public sealed partial class PuddleSystem : SharedPuddleSystem
{
    [Dependency] private AppearanceSystem _floodAppearance = default!;
    [Dependency] private FloodWaterlineSystem _floodWaterline = default!;
    [Dependency] private IconSmoothSystem _smooth = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <summary>
    ///     Share of a liquid's opacity it already has as a thin film. The rest comes with depth.
    /// </summary>
    private const float ShallowOpacity = 0.7f;
    private const float MaxDepthDarken = 0.4f;

    /// <summary>
    ///     The puddle sprite is grey, which darkens the colour it's tinted with. Flat water elsewhere has to match it.
    /// </summary>
    private const float SpriteShade = 0.69f;

    /// <summary>
    ///     How much of the liquid's opacity the local player sees, which drops while they're under it.
    /// </summary>
    private float _floodClarity = 1f;

    public void SetFloodClarity(float clarity)
    {
        _floodClarity = clarity;

        var query = EntityQueryEnumerator<PuddleComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance))
        {
            _floodAppearance.QueueUpdate(uid, appearance);
        }
    }

    [SubscribeLocalEvent]
    private void OnPuddleAppearance(EntityUid uid, PuddleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.TryGetData<float>(PuddleVisuals.CurrentVolume, out var volume))
            volume = 1.0f;

        // Update smoothing and sprite based on volume.
        if (volume < LowThreshold)
        {
            _sprite.LayerSetRsiState((uid, args.Sprite), component.PuddleKey, $"{component.PuddleState}a");
            _smooth.SetEnabled(uid, false, false);
        }
        else if (volume < MediumThreshold)
        {
            _sprite.LayerSetRsiState((uid, args.Sprite), component.PuddleKey, $"{component.PuddleState}b");
            _smooth.SetEnabled(uid, false, false);
        }
        else
        {
            _smooth.SetEnabled(uid, true);
        }

        var baseColor = Color.White;

        if (!args.TryGetData<Color>(PuddleVisuals.SolutionColor, out var color))
            color = args.Sprite.Color;

        if (!args.TryGetData<float>(PuddleVisuals.LiquidOpacity, out var opacity))
            opacity = color.A;

        // Deeper liquid gets darker and more opaque, reaching the liquid's own opacity at a full tile.
        var depth = MathF.Sqrt(Math.Clamp(volume * component.OverflowVolume.Float() / TileVolume.Float(), 0f, 1f));
        var shade = 1f - MaxDepthDarken * depth;
        var alpha = opacity * (ShallowOpacity + (1f - ShallowOpacity) * depth) * _floodClarity;
        color = new Color(color.R * shade, color.G * shade, color.B * shade, alpha);

        _sprite.SetColor((uid, args.Sprite), color * baseColor);
        _floodWaterline.SetLevel(uid, depth, new Color(color.R * SpriteShade, color.G * SpriteShade, color.B * SpriteShade, color.A), opacity);
    }

    #region Spill

    // Maybe someday we'll have clientside prediction for entity spawning, but not today.
    // Until then, these methods do nothing on the client.
    /// <inheritdoc/>
    public override bool TrySplashSpillAt(Entity<SpillableComponent?> entity, EntityCoordinates coordinates, out EntityUid puddleUid, out Solution solution, bool sound = true, EntityUid? user = null)
    {
        puddleUid = EntityUid.Invalid;
        solution = new Solution();
        return false;
    }

    public override bool TrySplashSpillAt(EntityUid entity,
        EntityCoordinates coordinates,
        Solution spilled,
        out EntityUid puddleUid,
        bool sound = true,
        EntityUid? user = null)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(EntityCoordinates coordinates, Solution solution, out EntityUid puddleUid, bool sound = true)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(EntityUid uid, Solution solution, out EntityUid puddleUid, bool sound = true, TransformComponent? transformComponent = null)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(TileRef tileRef, Solution solution, out EntityUid puddleUid, bool sound = true, bool tileReact = true)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    #endregion Spill
}
