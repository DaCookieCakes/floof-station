using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Floof.Persistence;
using Content.Shared._Floof.Persistence;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests.Tests._Floof.Persistence;

/// <summary>
///     YAML conversion and migrations of live character records.
/// </summary>
[TestFixture]
[TestOf(typeof(LiveCharacterManager))]
public sealed class LiveCharacterRecordTest : GameTest
{
    [Test]
    [Description("A record of every playable species survives being written to YAML and read back.")]
    public async Task YamlRoundTrip()
    {
        var manager = Server.ResolveDependency<LiveCharacterManager>();
        var serialization = Server.ResolveDependency<ISerializationManager>();
        var prototypes = Server.ResolveDependency<IPrototypeManager>();

        // Fixed seed so every run generates the same characters.
        Server.ResolveDependency<IRobustRandom>().SetSeed(1);

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var species in prototypes.EnumeratePrototypes<SpeciesPrototype>().Where(s => s.RoundStart))
                {
                    var profile = HumanoidCharacterProfile.RandomWithSpecies(species.ID);
                    var yaml = manager.Serialize(LiveCharacterRecord.FromProfile(profile));

                    var loaded = serialization.Read<LiveCharacterRecord>(LiveCharacterManager.Parse(yaml), notNullableOverride: true);
                    var applied = loaded.ApplyTo(new HumanoidCharacterProfile());

                    // Compared as YAML since colors lose float precision when written.
                    Assert.That(manager.Serialize(loaded), Is.EqualTo(yaml), $"{species.ID}: YAML changed after a round trip");
                    Assert.That(manager.Serialize(LiveCharacterRecord.FromProfile(applied)), Is.EqualTo(yaml),
                        $"{species.ID}: applying the record to a profile lost data");
                }
            }
        });
    }

    [Test]
    [Description("There is exactly one migration for every version below the current one.")]
    public void MigrationsComplete()
    {
        var manager = Server.ResolveDependency<LiveCharacterManager>();
        var expected = Enumerable.Range(1, LiveCharacterRecord.CurrentVersion - 1);

        Assert.That(manager.Migrations.Keys, Is.EquivalentTo(expected));
    }

    [Test]
    [Description("Unknown organ categories and markings are dropped instead of crashing the load; genital markings are kept.")]
    public async Task ValidateDropsUnknownIds()
    {
        var manager = Server.ResolveDependency<LiveCharacterManager>();
        var markingManager = Server.ResolveDependency<MarkingManager>();

        await Server.WaitAssertion(() =>
        {
            var record = LiveCharacterRecord.FromProfile(HumanoidCharacterProfile.DefaultWithSpecies("Human"));
            var organ = markingManager.GetOrgans("Human").Keys.First();

            record.Appearance.Markings["FloofTestMissingOrgan"] = new()
            {
                [HumanoidVisualLayers.Hair] = [new Marking("FloofTestMissingMarking", 1)],
            };
            record.Appearance.Markings[organ] = new()
            {
                [HumanoidVisualLayers.Hair] = [new Marking("FloofTestMissingMarking", 1)],
            };

            record.Appearance.Markings["Dick"] = new()
            {
                [HumanoidVisualLayers.Penis] =
                [
                    new Marking("Genital-Penis-Nondescript-1-0", 1),
                    new Marking("FloofTestMissingMarking", 1),
                ],
            };

            manager.Validate(record, "test");

            var markings = record.Appearance.Markings;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(markings, Does.Not.ContainKey(new ProtoId<OrganCategoryPrototype>("FloofTestMissingOrgan")));
                Assert.That(markings.Values.SelectMany(m => m.Values).SelectMany(l => l).Select(m => m.MarkingId.Id),
                    Does.Not.Contain("FloofTestMissingMarking"));
                Assert.That(markings["Dick"][HumanoidVisualLayers.Penis].Select(m => m.MarkingId.Id),
                    Is.EqualTo(new[] { "Genital-Penis-Nondescript-1-0" }), "Genital markings were not kept");
            }
        });
    }
}
