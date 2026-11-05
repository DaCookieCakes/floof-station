using Content.Client._Floof.Consent;
using Content.Shared._Floof.Consent.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private NewConsentText? _consentText;

    /// <summary>
    /// Refreshes the Consent text editor status.
    /// </summary>
    public void RefreshConsentText()
    {
        if (_consentText != null)
            return;

        _consentText = new NewConsentText();
        TabContainer.AddChild(_consentText);
        TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("humanoid-profile-editor-consent-tab"));

        _consentText.OnConsentTextChanged += OnConsentTextChange;
        _consentText.OnConsentToggleChanged += OnConsentToggle;
    }

    private void OnConsentTextChange(string content)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithConsentText(content);
        SetDirty();
    }

    private void OnConsentToggle(ProtoId<ConsentTogglePrototype> toggle, bool enabled)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithConsentToggle(toggle, enabled);
        SetDirty();
    }

    private void UpdateConsentTextEdit()
    {
        _consentText?.SetConsent(Profile?.ConsentText ?? "", Profile?.ConsentToggles ?? new HashSet<ProtoId<ConsentTogglePrototype>>());
    }
}
