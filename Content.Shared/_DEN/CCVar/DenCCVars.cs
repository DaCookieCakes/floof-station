using Robust.Shared.Configuration;

namespace Content.Shared._DEN.CCVar;

[CVarDefs]
public sealed class DenCCVars
{
    /// <summary>
    /// Stops the server from sending the station broadcast about people cryoing to this client.
    /// </summary>
    public static readonly CVarDef<bool> IgnoreCryoMessage =
        CVarDef.Create("den.ignore_cryo_message", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}