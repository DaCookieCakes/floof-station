using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

[CVarDefs]
public sealed partial class CCVars
{
    /// <summary>
    ///     Client-only, defines whether the client should predict leash joints.
    /// </summary>
    public static readonly CVarDef<bool> PredictLeashes =
        CVarDef.Create("leash.predict", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
