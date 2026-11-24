using Content.Shared._Floof.Sex;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void GenitalsInit()
    {
        Penis.OnToggled += args =>
        {
            SetGenitalsProfile(Profile?.WithPenis(args.Pressed));
            ReloadPreview();
            UpdateMarkings();
        };
        Vagina.OnToggled += args =>
        {
            SetGenitalsProfile(Profile?.WithVagina(args.Pressed));
            ReloadPreview();
            UpdateMarkings();
        };
        Breasts.OnToggled += args =>
        {
            SetGenitalsProfile(Profile?.WithBreasts(args.Pressed));
            ReloadPreview();
            UpdateMarkings();
        };
    }

    /// <summary>
    ///     Gives newly enabled genitals their default markings, like the server does on save.
    /// </summary>
    private void SetGenitalsProfile(HumanoidCharacterProfile? profile)
    {
        Profile = profile?.WithCharacterAppearance(
            GenitalMarkings.EnsureValid(profile.Appearance, profile.Genitals, profile.Sex));
    }

    private void SetGenitals(bool penis, bool vagina, bool breasts)
    {
        Penis.Pressed = penis;
        Vagina.Pressed = vagina;
        Breasts.Pressed = breasts;
    }

    private void UpdateGenitals()
    {
        if(Profile is null)
            return;
        SetGenitals(Profile.Genitals.Penis, Profile.Genitals.Vagina, Profile.Genitals.Breasts);
    }
}