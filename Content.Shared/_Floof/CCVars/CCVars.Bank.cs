using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;
public sealed partial class CCVars
{
    /// <summary>
    ///     Balance a character's bank account starts with.
    /// </summary>
    public static readonly CVarDef<int> BankStartingBalance =
        CVarDef.Create("bank.starting_balance", 5000, CVar.SERVERONLY);

    /// <summary>
    ///     Seconds between alive checks for paydays.
    /// </summary>
    public static readonly CVarDef<float> BankAliveCheckInterval =
        CVarDef.Create("bank.alive_check_interval", 300f, CVar.SERVERONLY);

    /// <summary>
    ///     Alive checks per payday.
    /// </summary>
    public static readonly CVarDef<int> BankChecksPerPayday =
        CVarDef.Create("bank.checks_per_payday", 6, CVar.SERVERONLY);
}
