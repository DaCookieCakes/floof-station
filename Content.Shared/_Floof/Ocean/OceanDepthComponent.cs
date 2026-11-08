using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Ocean;

/// <summary>
///     How deep this map is, 0 at the surface and 1 at the bottom. Only changes of direction are networked: everyone
///     works the current value out from these.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OceanDepthComponent : Component
{
    [DataField, AutoNetworkedField]
    public float StartDepth;

    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    /// <summary>
    ///     Depth gained per second. Negative on the way up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Rate;

    /// <summary>
    ///     The depth of the map in meters at 1. Flavour, as depth only follows the clock.
    /// </summary>
    [DataField]
    public float Meters = 600f;

    /// <summary>
    ///     The depth at which no daylight is left.
    /// </summary>
    [DataField]
    public float DarkAt = 0.5f;

    /// <summary>
    ///     The depth down to which the sea still looks like the surface did, so moving maps doesn't show.
    /// </summary>
    [DataField]
    public float SurfaceHoldTo = 0.004f;

    /// <summary>
    ///     The depth by which the surface has zoomed past and the water has climbed everything outside.
    /// </summary>
    [DataField]
    public float SubmergedFrom = 0.018f;

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
        return Math.Clamp(StartDepth + Rate * (float) (now - StartTime).TotalSeconds, 0f, 1f);
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
}
