using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void UpdateMarkings()
    {
        if (Profile == null)
        {
            return;
        }

        var dummy = SpriteView.PreviewDummy;
        var bodySystem = _entManager.System<BodySystem>();
        var organData = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData>();
        if (_entManager.TryGetComponent<BodyComponent>(dummy, out var body))
        {
            var organList = new OrganEnumerate(new List<Entity<OrganComponent>>());
            bodySystem.RelayEvent((dummy, body), ref organList);
            foreach (var (owner, organComp) in organList.Organs)
            {
                if (!_entManager.TryGetComponent<VisualOrganMarkingsComponent>(owner, out var comp))
                    continue;
                
                if(organComp.Category == null)
                    continue;
                
                organData.Add(organComp.Category.Value, comp.MarkingData);
            }
        }
        
        var organProfileData = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>(); 
        foreach (var organ in organData.Keys)
        {
            organProfileData[organ] = new()
            {
                Sex = Profile.Sex,
                EyeColor = Profile.Appearance.EyeColor,
                SkinColor = Profile.Appearance.SkinColor,
            };
        }

        _markingsModel.OrganProfileData = organProfileData; //951 ms
        _markingsModel.OrganData = organData; //770 ms
        _markingsModel.Markings = Profile.Appearance.Markings; 
    }

    private void OnMarkingChange()
    {
        if (Profile is null)
            return;

        Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithMarkings(_markingsModel.Markings));
        ReloadProfilePreview();
        SetDirty();
    }
}
