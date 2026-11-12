using Content.Client._Floof.Species.UI;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

// Floof - Species tab
public sealed partial class HumanoidProfileEditor
{
    private readonly SpeciesTab _speciesTab = new();

    // Stand-ins for the species row commented out of the appearance tab, so upstream code keeps compiling untouched.
    private OptionButton SpeciesButton = new();
    private TextureButton SpeciesInfoButton = new();

    private void SpeciesTabInit()
    {
        _speciesTab.OnSpeciesSelected += SetSpecies;

        // Put it in front of every other tab, but keep the appearance tab open by default.
        TabContainer.AddChild(_speciesTab);
        _speciesTab.SetPositionInParent(0);
        TabContainer.SetTabTitle(0, Loc.GetString("humanoid-profile-editor-species-tab"));
        _speciesTab.Visible = false;
        TabContainer.CurrentTab = 1;
    }

    private void UpdateSpeciesTab()
    {
        if (Profile != null)
            _speciesTab.SetSpecies(_species, Profile.Species);
    }
}
