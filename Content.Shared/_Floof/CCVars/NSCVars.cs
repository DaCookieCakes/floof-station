using Robust.Shared.Configuration;

namespace Content.Shared._Floof.CCVars;

[CVarDefs]
public sealed class NSCVars
{
    /// <summary>
    /// TODO: Figure out what this does, I'm porting all of this before doing any of the systems, so I have yet to touch any system code.
    ///
    /// Currently ported https://github.com/Fansana/floofstation1/pull/4
    /// </summary>
    public static readonly CVarDef<string> ConsentRules = CVarDef.Create("consent.consent_rules", "", CVar.ARCHIVE | CVar.CLIENTONLY);
}
