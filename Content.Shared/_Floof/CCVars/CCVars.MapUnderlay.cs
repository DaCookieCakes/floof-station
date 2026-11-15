using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

public sealed partial class CCVars
{
    /// <summary>
    ///     Shows the real map below through the water, such as the station coming up on a dive. Off draws a plain sea
    ///     floor instead, and the server sends nothing for it.
    /// </summary>
    public static readonly CVarDef<bool> MapUnderlay =
        CVarDef.Create("floof.map_underlay", true, CVar.ARCHIVE | CVar.CLIENT | CVar.REPLICATED);
}
