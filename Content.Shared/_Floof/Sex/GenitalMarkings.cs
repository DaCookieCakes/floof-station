using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.Sex;

/// <summary>
///     Validates genital markings. Genitals aren't species organs, so appearance validation skips them
///     and enabled genitals would otherwise spawn without their required default markings.
/// </summary>
public static class GenitalMarkings
{
    private static readonly (ProtoId<OrganCategoryPrototype> Category, EntProtoId Organ, Func<Genitals, bool> Enabled)[] Organs =
    [
        ("Dick", "OrganDick", g => g.Penis),
        ("Vagina", "OrganVagina", g => g.Vagina),
        ("Breasts", "OrganBreasts", g => g.Breasts),
    ];

    public static HumanoidCharacterAppearance EnsureValid(HumanoidCharacterAppearance appearance, Genitals genitals, Content.Shared.Humanoid.Sex sex)
    {
        var markingManager = IoCManager.Resolve<MarkingManager>();
        var markings = appearance.Markings.ShallowClone();

        foreach (var (category, organ, enabled) in Organs)
        {
            if (!enabled(genitals) || !markingManager.TryGetMarkingData(organ, out var data))
                continue;

            var actual = markings.GetValueOrDefault(category)?.ShallowClone() ?? new();
            markingManager.EnsureValidColors(actual);
            markingManager.EnsureValidGroupAndSex(actual, data.Value.Group, sex);
            markingManager.EnsureValidLayers(actual, data.Value.Layers);
            markingManager.EnsureValidLimits(actual, data.Value.Group, data.Value.Layers, appearance.SkinColor, appearance.EyeColor);
            markings[category] = actual;
        }

        return appearance.WithMarkings(markings);
    }
}
