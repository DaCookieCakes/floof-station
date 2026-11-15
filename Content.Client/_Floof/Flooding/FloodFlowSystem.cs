using System.Numerics;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Flooding;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Reads the same current mobs get pushed by off every wet tile, for the streaks on the water and the sound of it running.
/// </summary>
public sealed partial class FloodFlowSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private FloodWaterlineSystem _waterline = default!;
    [Dependency] private FloodLevelSystem _level = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier FlowSound = new SoundPathSpecifier("/Audio/_Floof/Effects/Flooding/flow_loop.ogg");

    /// <summary>
    ///     Puddles only change about once per flow, so there's no use measuring every frame.
    /// </summary>
    private const float MeasureInterval = 0.25f;

    /// <summary>
    ///     Seconds a tile's streaks and the sound take to follow a change.
    /// </summary>
    private const float FlowSmoothing = 1f;

    /// <summary>
    ///     Current on a tile where its streaks reach their full speed and brightness.
    /// </summary>
    public const float StrongCurrent = 0.3f;

    /// <summary>
    ///     Streak trips per second in the slightest current.
    /// </summary>
    private const float MinPhaseRate = 0.3f;

    /// <summary>
    ///     Current summed over the tiles in earshot, closer ones counting more, where the sound is at its loudest.
    /// </summary>
    private const float LoudCurrent = 3f;

    private const float SilentGain = 0.01f;

    /// <summary>
    ///     How far inside the edge of what the server sends us a tile has to be, for its neighbours to be in there too.
    /// </summary>
    private const float RangeMargin = 1.5f;

    /// <summary>
    ///     Seconds a tile has to be in range before it's measured. Puddles don't all arrive at once.
    /// </summary>
    private const float ArrivalTime = 1f;

    private float _minDepth;
    private float _streakSpeed;
    private float _soundVolume;
    private float _soundRange;
    private float _ambienceVolume;
    private float _pvsRange;

    private float _sinceMeasure;
    private float _gain;
    private float _targetGain;
    private EntityUid? _stream;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.FloodFlowMinDepth, value => _minDepth = value, true);
        Subs.CVar(_cfg, CCVars.FloodFlowStreakSpeed, value => _streakSpeed = value, true);
        Subs.CVar(_cfg, CCVars.FloodFlowSoundVolume, value => _soundVolume = value, true);
        Subs.CVar(_cfg, CCVars.FloodFlowSoundRange, value => _soundRange = value, true);
        Subs.CVar(_cfg, Content.Shared.CCVar.CCVars.AmbienceVolume, value => _ambienceVolume = SharedAudioSystem.GainToVolume(value), true);

        Subs.CVar(_cfg, CVars.NetMaxUpdateRange, value => _pvsRange = value, true);

        _overlay.AddOverlay(new FloodFlowOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<FloodFlowOverlay>();
        _stream = _audio.Stop(_stream);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _sinceMeasure += frameTime;
        var measure = _sinceMeasure >= MeasureInterval;
        if (measure)
            _sinceMeasure = 0f;

        var blend = 1f - MathF.Exp(-frameTime / FlowSmoothing);
        foreach (var (gridUid, tiles) in _waterline.Grids)
        {
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            foreach (var (indices, tile) in tiles)
            {
                if (measure)
                    tile.FlowTarget = Measure(gridUid, grid, indices, tile);

                tile.Flow = Vector2.Lerp(tile.Flow, tile.FlowTarget, blend);
                var share = MathF.Min(tile.Flow.Length() / StrongCurrent, 1f);
                tile.FlowPhase = (tile.FlowPhase + MathHelper.Lerp(MinPhaseRate, _streakSpeed, share) * frameTime) % 1f;
            }
        }

        if (measure)
            _targetGain = MeasureGain();

        UpdateSound(blend);
    }

    private Vector2 Measure(EntityUid gridUid, MapGridComponent grid, Vector2i indices, FloodWaterlineSystem.WetTile tile)
    {
        // A neighbour the server hasn't sent us yet looks just like a dry floor.
        if (!IsInRange(gridUid, grid, indices))
        {
            tile.InRange = 0f;
            return Vector2.Zero;
        }

        tile.InRange += MeasureInterval;
        if (tile.InRange < ArrivalTime || tile.Depth < _minDepth)
            return Vector2.Zero;

        var volume = _level.GetVolume(gridUid, grid, indices);
        return volume > 0f ? _level.GetCurrent(gridUid, grid, indices, volume) : Vector2.Zero;
    }

    private bool IsInRange(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (_player.LocalEntity is not { } player)
            return false;

        var offset = _map.LocalToWorld(gridUid, grid, indices + Vector2Helpers.Half) - _transform.GetWorldPosition(player);
        var range = _pvsRange / 2f - RangeMargin;
        return MathF.Abs(offset.X) <= range && MathF.Abs(offset.Y) <= range;
    }

    private float MeasureGain()
    {
        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out TransformComponent? xform) ||
            xform.GridUid is not { } gridUid ||
            !_waterline.Grids.TryGetValue(gridUid, out var tiles) ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return 0f;
        }

        var listener = _map.WorldToLocal(gridUid, grid, _transform.GetWorldPosition(xform));
        var total = 0f;
        foreach (var (indices, tile) in tiles)
        {
            var distance = (indices + Vector2Helpers.Half - listener).Length();
            if (distance < _soundRange)
                total += tile.FlowTarget.Length() * (1f - distance / _soundRange);
        }

        return MathF.Min(total / LoudCurrent, 1f);
    }

    private void UpdateSound(float blend)
    {
        _gain = MathHelper.Lerp(_gain, _targetGain, blend);

        if (_gain < SilentGain)
        {
            _stream = _audio.Stop(_stream);
            return;
        }

        var volume = SharedAudioSystem.GainToVolume(_gain) + _ambienceVolume + _soundVolume;
        if (_stream == null || !Exists(_stream))
            _stream = _audio.PlayGlobal(FlowSound, Filter.Local(), false, AudioParams.Default.WithLoop(true).WithVolume(volume))?.Entity;
        else
            _audio.SetVolume(_stream, volume);
    }
}
