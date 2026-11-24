using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

public sealed partial class CCVars
{
    /// <summary>
    ///     Live character backups always kept per character, regardless of age.
    /// </summary>
    public static readonly CVarDef<int> LiveCharacterHistoryMin =
        CVarDef.Create("persistence.history_min", 7, CVar.SERVERONLY);

    /// <summary>
    ///     Live character backups younger than this many days are never pruned.
    /// </summary>
    public static readonly CVarDef<int> LiveCharacterHistoryDays =
        CVarDef.Create("persistence.history_days", 30, CVar.SERVERONLY);

    /// <summary>
    ///     Seconds after death until a ghost can respawn from the ghost menu.
    /// </summary>
    public static readonly CVarDef<float> GhostRespawnTime =
        CVarDef.Create("persistence.ghost_respawn_time", 300f, CVar.SERVERONLY);
}
