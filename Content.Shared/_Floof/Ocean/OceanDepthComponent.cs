using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Ocean;

/// <summary>
///     How deep this map is in meters, 0 at the surface and <see cref="FloorDepth"/> at the bottom. Only changes of
///     direction are networked: everyone works the current value out from these.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OceanDepthComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StartDepth;

    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    /// <summary>
    ///     Meters gained per second. Negative on the way up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Rate;

    /// <summary>
    ///     How deep the sea floor lies, as far down as this map goes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FloorDepth = 600f;

    /// <summary>
    ///     The depth at which no daylight is left.
    /// </summary>
    [DataField]
    public float DarkAt = 300f;

    /// <summary>
    ///     The depth down to which the sea still looks like the surface did, so moving maps doesn't show.
    /// </summary>
    [DataField]
    public float SurfaceHoldTo = 2.4f;

    /// <summary>
    ///     The depth by which the surface has zoomed past and the water has climbed everything outside.
    /// </summary>
    [DataField]
    public float SubmergedFrom = 10.8f;

    /// <summary>
    ///     How far above the floor it starts to show through the water.
    /// </summary>
    [DataField]
    public float FloorSeenFrom = 102f;

    /// <summary>
    ///     How much smaller the floor looks when it first shows, growing to its real size by the landing.
    /// </summary>
    [DataField]
    public float FloorFarZoom = 2f;

    /// <summary>
    ///     The map's light at the surface. It fades along with the water.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color SurfaceLight = Color.Black;

    /// <summary>
    ///     The map the station lands on, resting at full depth. The dive's own progress is on the map it travels through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Seabed;

    public float GetDepth(TimeSpan now)
    {
        return Math.Clamp(StartDepth + Rate * (float) (now - StartTime).TotalSeconds, 0f, FloorDepth);
    }

    /// <summary>
    ///     How much daylight reaches this far down, 1 at the surface and 0 in the dark.
    /// </summary>
    public float GetBrightness(TimeSpan now)
    {
        var share = Math.Clamp(GetDepth(now) / DarkAt, 0f, 1f);
        var light = 1f - share * share * (3f - 2f * share);
        return light * light;
    }

    /// <summary>
    ///     How far the station has gone under, 0 while it still looks like the surface and 1 once the water covers it all.
    /// </summary>
    public float GetSubmerged(TimeSpan now)
    {
        var share = Math.Clamp((GetDepth(now) - SurfaceHoldTo) / (SubmergedFrom - SurfaceHoldTo), 0f, 1f);
        return share * share * (3f - 2f * share);
    }

    /// <summary>
    ///     How close the floor has come, 0 until it shows and 1 on it. A larger range counts it as showing from further up.
    /// </summary>
    public float GetLanding(TimeSpan now, float range = 1f)
    {
        return Math.Clamp(1f - (FloorDepth - GetDepth(now)) / (FloorSeenFrom * range), 0f, 1f);
    }
}
