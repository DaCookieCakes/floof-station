namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    public HumanoidCharacterProfile WithGenitals(Genitals genitals)
    {
        return new(this) { Genitals = genitals };
    }

    public HumanoidCharacterProfile WithPenis(bool penis)
    {
        return new(this) { Genitals = Genitals with { Penis = penis } };
    }

    public HumanoidCharacterProfile WithVagina(bool vagina)
    {
        return new(this) { Genitals = Genitals with { Vagina = vagina } };
    }

    public HumanoidCharacterProfile WithBreasts(bool breasts)
    {
        return new(this) { Genitals = Genitals with { Breasts = breasts } };
    }
}
