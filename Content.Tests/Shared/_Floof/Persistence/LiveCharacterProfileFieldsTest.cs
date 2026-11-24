using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Shared._Floof.Persistence;
using Content.Shared.Preferences;
using NUnit.Framework;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Tests.Shared._Floof.Persistence;

/// <summary>
///     Fails when a data field is added to or removed from <see cref="HumanoidCharacterProfile"/>,
///     so every field gets a deliberate decision on whether the live character keeps it.
/// </summary>
[TestFixture]
[TestOf(typeof(HumanoidCharacterProfile))]
public sealed class LiveCharacterProfileFieldsTest
{
    /// <summary>
    ///     Fields stored on the live character.
    /// </summary>
    private static readonly HashSet<string> Persisted =
    [
        "Name",
        "Genitals",
        "Species",
        "Age",
        "Sex",
        "Voice",
        "Gender",
        "Height",
        "Width",
        "Appearance",
    ];

    /// <summary>
    ///     Fields that always come from the template.
    /// </summary>
    private static readonly HashSet<string> Ignored =
    [
        "_jobPriorities",
        "_antagPreferences",
        "_traitPreferences",
        "_loadouts",
        "FlavorText",
        "ConsentText",
        "_consentToggles",
        "SpawnPriority",
        "PreferenceUnavailable",
    ];

    private static HashSet<string> DataFields(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        return type.GetFields(flags)
            .Cast<MemberInfo>()
            .Concat(type.GetProperties(flags))
            .Where(m => m.GetCustomAttribute<DataFieldAttribute>() != null)
            .Select(m => m.Name)
            .ToHashSet();
    }

    [Test]
    public void RecordMatchesPersisted()
    {
        Assert.That(DataFields(typeof(LiveCharacterRecord)), Is.EquivalentTo(Persisted),
            "LiveCharacterRecord fields must match Persisted; changing them needs a version bump and a migration");
    }

    [Test]
    public void AllFieldsClassified()
    {
        var fields = DataFields(typeof(HumanoidCharacterProfile));

        var known = Persisted.Union(Ignored).ToHashSet();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields.Except(known), Is.Empty,
                "New profile fields: add them to Persisted (and the live character record) or Ignored");
            Assert.That(known.Except(fields), Is.Empty,
                "Removed profile fields: remove them from the lists and add a live character migration if persisted");
            Assert.That(Persisted.Intersect(Ignored), Is.Empty, "Field is both persisted and ignored");
        }
    }
}
