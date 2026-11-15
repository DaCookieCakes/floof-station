using System.Numerics;
using Content.Shared._EE.HeightAdjust;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem : EntitySystem
{
    [Dependency] private GrammarSystem _grammar = default!;
    [Dependency] private HeightAdjustSystem _heightAdjust = default!; // Goobstation: port EE height/width sliders

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, ExaminedEvent>(OnExamined);
    }

    public void ApplyProfileTo(Entity<HumanoidProfileComponent?> ent, HumanoidCharacterProfile profile)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.Gender = profile.Gender;
        ent.Comp.Age = profile.Age;
        ent.Comp.Species = profile.Species;
        ent.Comp.Voice = profile.Voice;
        ent.Comp.Sex = profile.Sex;
        Dirty(ent);

        // begin Goobstation: port EE height/width sliders
        var species = ProtoMan.Index(ent.Comp.Species);
        if (profile.Height <= 0 || profile.Width <= 0)
            SetScale(ent, new Vector2(species.DefaultWidth, species.DefaultHeight), true, ent.Comp);
        else
            SetScale(ent, new Vector2(profile.Width, profile.Height), true, ent.Comp);

        _heightAdjust.SetScale(ent, new Vector2(ent.Comp.Width, ent.Comp.Height));
        // end Goobstation: port EE height/width sliders

        var voiceChanged = new VoiceChangedEvent(ent.Comp.Voice, profile.Voice);
        RaiseLocalEvent(ent, ref voiceChanged);

        if (TryComp<GrammarComponent>(ent, out var grammar))
        {
            _grammar.SetGender((ent, grammar), profile.Gender);
        }
    }

    // begin Goobstation: port EE height/width sliders
    /// <summary>
    ///     Set the scale of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="scale">The scale to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid profile component of the entity</param>
    public void SetScale(EntityUid uid, Vector2 scale, bool sync = true, HumanoidProfileComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        var species = ProtoMan.Index(humanoid.Species);
        humanoid.Height = Math.Clamp(scale.Y, species.MinHeight, species.MaxHeight);
        humanoid.Width = Math.Clamp(scale.X, species.MinWidth, species.MaxWidth);

        if (sync)
            Dirty(uid, humanoid);
    }
    // end Goobstation: port EE height/width sliders

    private void OnExamined(Entity<HumanoidProfileComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Examinable)
            return;

        var identity = Identity.Entity(ent, EntityManager);
        var species = GetSpeciesRepresentation(ent.Comp.Species).ToLower();
        var age = GetAgeRepresentation(ent.Comp.Species, ent.Comp.Age);

        args.PushText(Loc.GetString("humanoid-appearance-component-examine", ("user", identity), ("age", age), ("species", species)));
    }

    /// <summary>
    /// Takes ID of the species prototype, returns UI-friendly name of the species.
    /// </summary>
    public string GetSpeciesRepresentation(ProtoId<SpeciesPrototype> species)
    {
        if (ProtoMan.TryIndex(species, out var speciesPrototype))
            return Loc.GetString(speciesPrototype.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    /// <summary>
    /// Takes ID of the species prototype and an age, returns an approximate description
    /// </summary>
    public string GetAgeRepresentation(ProtoId<SpeciesPrototype> species, int age)
    {
        if (!ProtoMan.TryIndex(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
        {
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.OldAge)
        {
            return Loc.GetString("identity-age-middle-aged");
        }

        return Loc.GetString("identity-age-old");
    }
}
