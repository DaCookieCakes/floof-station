using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

public sealed partial class CCVars
{
    /// <summary>
    ///     Seconds between two flows of the same overflowing puddle. The work is spread evenly over the ticks in between.
    /// </summary>
    public static readonly CVarDef<float> FloodFlowInterval =
        CVarDef.Create("flood.flow_interval", 1f, CVar.SERVERONLY);

    /// <summary>
    ///     Main-thread milliseconds puddle flow may use per tick before the rest waits for the next one.
    /// </summary>
    public static readonly CVarDef<float> FloodMaxProcessTime =
        CVarDef.Create("flood.max_process_time", 2f, CVar.SERVERONLY);

    /// <summary>
    ///     Depth (share of a full tile's height) where wading starts to slow you down.
    /// </summary>
    public static readonly CVarDef<float> FloodSlowStartDepth =
        CVarDef.Create("flood.slow_start_depth", 0.2f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Depth where wading has slowed to a crawl.
    /// </summary>
    public static readonly CVarDef<float> FloodSlowEndDepth =
        CVarDef.Create("flood.slow_end_depth", 0.4f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Share of your speed left at a crawl.
    /// </summary>
    public static readonly CVarDef<float> FloodSlowSpeed =
        CVarDef.Create("flood.slow_speed", 0.45f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Depth where a current can start to carry you.
    /// </summary>
    public static readonly CVarDef<float> FloodSweepDepth =
        CVarDef.Create("flood.sweep_depth", 0.15f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Current needed to carry you at the sweep depth, as half the share of the deeper tile's liquid two neighbours differ by (a dry tile next door is 0.5).
    /// </summary>
    public static readonly CVarDef<float> FloodSweepSurge =
        CVarDef.Create("flood.sweep_surge", 0.25f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Current needed to carry you at swimming depth. Between the two depths it scales linearly.
    /// </summary>
    public static readonly CVarDef<float> FloodSweepCalm =
        CVarDef.Create("flood.sweep_calm", 0.03f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Mob speed per unit of current.
    /// </summary>
    public static readonly CVarDef<float> FloodCurrentScale =
        CVarDef.Create("flood.current_scale", 8f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     The fastest a current carries a mob. Above sprint speed nobody can fight it.
    /// </summary>
    public static readonly CVarDef<float> FloodCurrentMaxSpeed =
        CVarDef.Create("flood.current_max_speed", 6f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     How quickly a current gets a mob up to its speed.
    /// </summary>
    public static readonly CVarDef<float> FloodCurrentAcceleration =
        CVarDef.Create("flood.current_acceleration", 4f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Seconds a mob takes to ease into a change in the current. Higher feels smoother but reacts later.
    /// </summary>
    public static readonly CVarDef<float> FloodCurrentSmoothing =
        CVarDef.Create("flood.current_smoothing", 1f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Share of the deeper tile two neighbours may differ by and still count as level, so settled water has no current.
    /// </summary>
    public static readonly CVarDef<float> FloodCurrentDeadzone =
        CVarDef.Create("flood.current_deadzone", 0.05f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Depth where you stop wading and start swimming.
    /// </summary>
    public static readonly CVarDef<float> FloodSwimDepth =
        CVarDef.Create("flood.swim_depth", 0.5f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Whether someone lying down can swim in shallower liquid than someone standing.
    /// </summary>
    public static readonly CVarDef<bool> FloodLyingSwim =
        CVarDef.Create("flood.lying_swim", true, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Depth where you start swimming if you're lying down.
    /// </summary>
    public static readonly CVarDef<float> FloodLyingSwimDepth =
        CVarDef.Create("flood.lying_swim_depth", 0.25f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Speed a stroke pushes you up to in liquid too shallow to swim in standing, so lying down doesn't beat wading.
    /// </summary>
    public static readonly CVarDef<float> FloodLyingStrokeSpeed =
        CVarDef.Create("flood.lying_stroke_speed", 1.6f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Seconds from one stroke to the next while holding a direction.
    /// </summary>
    public static readonly CVarDef<float> FloodStrokeInterval =
        CVarDef.Create("flood.stroke_interval", 0.8f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Seconds of each stroke spent pushing. The rest of the interval is glide.
    /// </summary>
    public static readonly CVarDef<float> FloodStrokeTime =
        CVarDef.Create("flood.stroke_time", 0.25f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Speed a stroke pushes you up to.
    /// </summary>
    public static readonly CVarDef<float> FloodStrokeSpeed =
        CVarDef.Create("flood.stroke_speed", 2.4f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     How quickly a stroke reaches its speed.
    /// </summary>
    public static readonly CVarDef<float> FloodStrokeAcceleration =
        CVarDef.Create("flood.stroke_acceleration", 15f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Friction while swimming. Lower glides further between strokes.
    /// </summary>
    public static readonly CVarDef<float> FloodGlideFriction =
        CVarDef.Create("flood.glide_friction", 1.6f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Depth where moving liquid starts to show streaks and make noise. Thin films creeping across a floor stay quiet.
    /// </summary>
    public static readonly CVarDef<float> FloodFlowMinDepth =
        CVarDef.Create("flood.flow_min_depth", 0.1f, CVar.CLIENTONLY);

    /// <summary>
    ///     How fast the streaks run, as trips across a tile per second in a strong current.
    /// </summary>
    public static readonly CVarDef<float> FloodFlowStreakSpeed =
        CVarDef.Create("flood.flow_streak_speed", 1.5f, CVar.CLIENTONLY);

    /// <summary>
    ///     Opacity of a streak at its brightest.
    /// </summary>
    public static readonly CVarDef<float> FloodFlowStreakOpacity =
        CVarDef.Create("flood.flow_streak_opacity", 0.6f, CVar.CLIENTONLY);

    /// <summary>
    ///     Volume of moving liquid at its loudest, on top of the ambience slider.
    /// </summary>
    public static readonly CVarDef<float> FloodFlowSoundVolume =
        CVarDef.Create("flood.flow_sound_volume", 0f, CVar.CLIENTONLY);

    /// <summary>
    ///     Tiles away moving liquid can still be heard.
    /// </summary>
    public static readonly CVarDef<float> FloodFlowSoundRange =
        CVarDef.Create("flood.flow_sound_range", 10f, CVar.CLIENTONLY);

    /// <summary>
    ///     Difference in depth, as a fraction of a tile, across a firelock at which it drops and locks.
    /// </summary>
    public static readonly CVarDef<float> FloodFirelockDepth =
        CVarDef.Create("flood.firelock_depth", 0.1f, CVar.SERVERONLY);

    /// <summary>
    ///     Depth, as a fraction of a tile, at which even a swimmer's head goes under.
    /// </summary>
    public static readonly CVarDef<float> FloodDrownDepth =
        CVarDef.Create("flood.drown_depth", 0.9f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Share of the stroke speed left to someone swimming in a suit that sinks them.
    /// </summary>
    public static readonly CVarDef<float> FloodSinkStroke =
        CVarDef.Create("flood.sink_stroke", 0.5f, CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    ///     Stamina damage per second of swimming.
    /// </summary>
    public static readonly CVarDef<float> FloodSwimStamina =
        CVarDef.Create("flood.swim_stamina", 2.5f, CVar.SERVERONLY);

    /// <summary>
    ///     Units per second of the liquid that touch a mob at a full tile's depth, less the shallower it is.
    /// </summary>
    public static readonly CVarDef<float> FloodTouchRate =
        CVarDef.Create("flood.touch_rate", 2f, CVar.SERVERONLY);

    /// <summary>
    ///     Units per second a submerged mob swallows.
    /// </summary>
    public static readonly CVarDef<float> FloodSwallowRate =
        CVarDef.Create("flood.swallow_rate", 1f, CVar.SERVERONLY);

    /// <summary>
    ///     Depth at which liquid that doesn't burn puts out fires.
    /// </summary>
    public static readonly CVarDef<float> FloodExtinguishDepth =
        CVarDef.Create("flood.extinguish_depth", 0.1f, CVar.SERVERONLY);

    /// <summary>
    ///     Slowest current that still carries loose items.
    /// </summary>
    public static readonly CVarDef<float> FloodItemDriftCurrent =
        CVarDef.Create("flood.item_drift_current", 0.05f, CVar.SERVERONLY);

    /// <summary>
    ///     Fastest a loose item drifts, in tiles per second.
    /// </summary>
    public static readonly CVarDef<float> FloodItemDriftSpeed =
        CVarDef.Create("flood.item_drift_speed", 1f, CVar.SERVERONLY);
}
