using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Floof.Sex;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Floof.Sex;

[TestFixture]
[TestOf(typeof(GenitalMarkings))]
public sealed class GenitalMarkingsTest : GameTest
{
    private static readonly (ProtoId<OrganCategoryPrototype> Organ, HumanoidVisualLayers Layer)[] Organs =
    [
        ("Dick", HumanoidVisualLayers.Penis),
        ("Vagina", HumanoidVisualLayers.Vagina),
        ("Breasts", HumanoidVisualLayers.Breasts),
    ];

    private static readonly ProtoId<MarkingsGroupPrototype> Genital = "Genital";

    [Test]
    [Description("Enabled genitals without markings get their group's defaults; disabled ones get nothing.")]
    public async Task EnabledGenitalsGetDefaults()
    {
        var group = SProtoMan.Index(Genital);

        await Server.WaitAssertion(() =>
        {
            var appearance = new HumanoidCharacterAppearance(Color.Black, Color.White, new());
            var all = GenitalMarkings.EnsureValid(appearance, new Genitals { Penis = true, Vagina = true, Breasts = true }, Content.Shared.Humanoid.Sex.Unsexed);
            var none = GenitalMarkings.EnsureValid(appearance, new Genitals(), Content.Shared.Humanoid.Sex.Unsexed);

            using (Assert.EnterMultipleScope())
            {
                foreach (var (organ, layer) in Organs)
                {
                    var markings = all.Markings.GetValueOrDefault(organ)?.GetValueOrDefault(layer);
                    Assert.That(markings?.Select(m => m.MarkingId), Is.EqualTo(group.Limits[layer].Default), $"{organ} has no default marking");
                    Assert.That(none.Markings.ContainsKey(organ), Is.False, $"Disabled {organ} got markings");
                }
            }
        });
    }
}
