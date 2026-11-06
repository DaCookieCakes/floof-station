using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

[DataDefinition, Serializable, NetSerializable]
public sealed partial record Genitals
{
    [DataField]
    public bool Vagina { get; set; }

    [DataField]
    public bool Penis { get; set; }

    [DataField]
    public bool Breasts { get; set; }

    /// <summary>
    /// The genitals a new character of the given sex starts with.
    /// </summary>
    public static Genitals DefaultForSex(Sex sex)
    {
        return sex switch
        {
            Sex.Male => new Genitals { Penis = true },
            Sex.Female => new Genitals { Vagina = true, Breasts = true },
            _ => new Genitals(),
        };
    }
}
