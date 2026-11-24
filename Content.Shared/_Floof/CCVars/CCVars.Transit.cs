using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> TransitPhantomReach =
        CVarDef.Create("transit.phantom_reach", 50f, CVar.SERVERONLY);

    public static readonly CVarDef<float> TransitDockRange =
        CVarDef.Create("transit.dock_range", 15f, CVar.SERVERONLY);
}
