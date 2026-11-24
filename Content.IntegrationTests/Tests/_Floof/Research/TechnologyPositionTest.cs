using System.Collections.Generic;
using System.Linq;
using Content.Shared._Floof.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Floof.Research;

[TestFixture]
public sealed class TechnologyPositionTest
{
    /// <summary>
    /// Every visible technology needs exactly one spot on the tech tree.
    /// Duplicate positions are covered by TechnologyPrototypePositionTests.
    /// </summary>
    [Test]
    public async Task TechnologyPositionsAreValidTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var placed = new HashSet<ProtoId<TechnologyPrototype>>();

            Assert.Multiple(() =>
            {
                foreach (var position in protoManager.EnumeratePrototypes<TechnologyPositionPrototype>())
                {
                    Assert.That(placed.Add(position.Technology),
                        $"Technology {position.Technology} has more than one {nameof(TechnologyPositionPrototype)}");
                }

                foreach (var tech in protoManager.EnumeratePrototypes<TechnologyPrototype>().Where(t => !t.Hidden))
                {
                    Assert.That(placed, Does.Contain(new ProtoId<TechnologyPrototype>(tech.ID)),
                        $"Technology {tech.ID} has no {nameof(TechnologyPositionPrototype)}, so it won't show in the research console");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The tree's prerequisites should replace the technology's own, since that is what research checks use.
    /// </summary>
    [Test]
    public async Task TechnologyPrerequisitesFollowTreeTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var position in protoManager.EnumeratePrototypes<TechnologyPositionPrototype>())
                {
                    var tech = protoManager.Index(position.Technology);
                    Assert.That(tech.TechnologyPrerequisites, Is.EquivalentTo(position.Prerequisites),
                        $"Technology {tech.ID} prerequisites don't match its {nameof(TechnologyPositionPrototype)}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
