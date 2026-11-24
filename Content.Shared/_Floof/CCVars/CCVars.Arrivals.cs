using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> ArrivalsDiveTime =
        CVarDef.Create("shuttle.arrivals_dive_time", 150f, CVar.SERVERONLY);
}
