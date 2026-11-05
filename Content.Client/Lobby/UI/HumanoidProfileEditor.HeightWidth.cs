// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Physics;

namespace Content.Client.Lobby.UI;

// Goobstation: port EE height/width sliders
public sealed partial class HumanoidProfileEditor
{
    private enum SliderUpdate
    {
        Height,
        Width,
        Both
    }

    private void HeightWidthInit()
    {
        UpdateHeightWidthSliders();

        HeightSlider.OnValueChanged += _ => UpdateDimensions(SliderUpdate.Height);
        WidthSlider.OnValueChanged += _ => UpdateDimensions(SliderUpdate.Width);

        HeightReset.OnPressed += _ => HeightSlider.Value = GetHeightWidthSpecies().DefaultHeight;
        WidthReset.OnPressed += _ => WidthSlider.Value = GetHeightWidthSpecies().DefaultWidth;
    }

    private SpeciesPrototype GetHeightWidthSpecies()
    {
        return _species.Find(x => x.ID == Profile?.Species) ?? _species.First();
    }

    private void UpdateHeightWidthSliders()
    {
        if (Profile is null)
            return;

        var species = GetHeightWidthSpecies();

        HeightSlider.MinValue = species.MinHeight;
        HeightSlider.MaxValue = species.MaxHeight;
        HeightSlider.SetValueWithoutEvent(Profile.Height);

        WidthSlider.MinValue = species.MinWidth;
        WidthSlider.MaxValue = species.MaxWidth;
        WidthSlider.SetValueWithoutEvent(Profile.Width);

        UpdateDimensions(SliderUpdate.Both);
    }

    private void UpdateDimensions(SliderUpdate updateType)
    {
        if (Profile == null)
            return;

        var species = GetHeightWidthSpecies();

        var heightValue = Math.Clamp(HeightSlider.Value, species.MinHeight, species.MaxHeight);
        var widthValue = Math.Clamp(WidthSlider.Value, species.MinWidth, species.MaxWidth);
        var sizeRatio = species.SizeRatio;
        var ratio = heightValue / widthValue;

        if (updateType == SliderUpdate.Height || updateType == SliderUpdate.Both)
            if (ratio < 1 / sizeRatio || ratio > sizeRatio)
                widthValue = heightValue / (ratio < 1 / sizeRatio ? (1 / sizeRatio) : sizeRatio);

        if (updateType == SliderUpdate.Width || updateType == SliderUpdate.Both)
            if (ratio < 1 / sizeRatio || ratio > sizeRatio)
                heightValue = widthValue * (ratio < 1 / sizeRatio ? (1 / sizeRatio) : sizeRatio);

        heightValue = Math.Clamp(heightValue, species.MinHeight, species.MaxHeight);
        widthValue = Math.Clamp(widthValue, species.MinWidth, species.MaxWidth);

        // Floof Section - HeightWidth
        HeightSlider.SetValueWithoutEvent(heightValue);
        WidthSlider.SetValueWithoutEvent(widthValue);

        if (Profile.Height != heightValue || Profile.Width != widthValue)
        {
            Profile = Profile.WithHeight(heightValue).WithWidth(widthValue);
            ReloadProfilePreview();
        }
        // Floof Section End

        var height = MathF.Round(species.AverageHeight * HeightSlider.Value);
        HeightLabel.Text = Loc.GetString("humanoid-profile-editor-height-label", ("height", (int) height));

        var width = MathF.Round(species.AverageWidth * WidthSlider.Value);
        WidthLabel.Text = Loc.GetString("humanoid-profile-editor-width-label", ("width", (int) width));

        UpdateWeight();
    }

    private void UpdateWeight()
    {
        if (Profile == null)
            return;

        var species = GetHeightWidthSpecies();
        // TODO: Remove obsolete method
        _prototypeManager.Index(species.Prototype).TryGetComponent<FixturesComponent>(out var fixture);

        if (fixture != null)
        {
            var radius = fixture.Fixtures["fix1"].Shape.Radius;
            var density = fixture.Fixtures["fix1"].Density;
            var avg = (Profile.Width + Profile.Height) / 2;
            var weight = MathF.Round(MathF.PI * MathF.Pow(radius * avg, 2) * density);
            WeightLabel.Text = Loc.GetString("humanoid-profile-editor-weight-label", ("weight", (int) weight));
        }
        else // Whelp, the fixture doesn't exist, guesstimate it instead
            WeightLabel.Text = Loc.GetString("humanoid-profile-editor-weight-label", ("weight", (int) 71));

        SpriteView.InvalidateMeasure();
    }
}
