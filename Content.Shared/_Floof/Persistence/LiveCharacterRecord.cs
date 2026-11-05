using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Floof.Persistence;

/// <summary>
///     The persisted state of a live character, stored as YAML in the database.
///     Applied on top of the template profile when spawning.
/// </summary>
/// <remarks>
///     Changing the fields requires bumping <see cref="CurrentVersion"/> and adding a migration.
/// </remarks>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class LiveCharacterRecord
{
    public const int CurrentVersion = 1;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public ProtoId<SpeciesPrototype> Species = HumanoidCharacterProfile.DefaultSpecies;

    [DataField]
    public int Age;

    [DataField]
    public Content.Shared.Humanoid.Sex Sex;

    [DataField]
    public ProtoId<EmoteSoundsPrototype> Voice = HumanoidCharacterProfile.DefaultVoice;

    [DataField]
    public Gender Gender;

    [DataField]
    public float Height = 1f;

    [DataField]
    public float Width = 1f;

    [DataField]
    public HumanoidCharacterAppearance Appearance = new();

    [DataField]
    public Genitals Genitals = new();

    public static LiveCharacterRecord FromProfile(HumanoidCharacterProfile profile)
    {
        return new LiveCharacterRecord
        {
            Name = profile.Name,
            Species = profile.Species,
            Age = profile.Age,
            Sex = profile.Sex,
            Voice = profile.Voice,
            Gender = profile.Gender,
            Height = profile.Height,
            Width = profile.Width,
            Appearance = profile.Appearance,
            Genitals = profile.Genitals,
        };
    }

    /// <summary>
    ///     Returns the template with this record's fields applied. Everything else comes from the template.
    /// </summary>
    public HumanoidCharacterProfile ApplyTo(HumanoidCharacterProfile template)
    {
        return template
            .WithName(Name)
            .WithSpecies(Species)
            .WithAge(Age)
            .WithSex(Sex)
            .WithVoice(Voice)
            .WithGender(Gender)
            .WithHeight(Height)
            .WithWidth(Width)
            .WithCharacterAppearance(Appearance)
            .WithGenitals(Genitals);
    }
}
