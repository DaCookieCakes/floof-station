using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> SubPhantomReach =
        CVarDef.Create("sub.phantom_reach", 50f, CVar.SERVERONLY);

    public static readonly CVarDef<float> SubDockRange =
        CVarDef.Create("sub.dock_range", 15f, CVar.SERVERONLY);
}
