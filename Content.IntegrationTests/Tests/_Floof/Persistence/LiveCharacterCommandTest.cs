using System.Collections.Generic;
using System.Linq;
using Content.Client._Floof.Persistence;
using Content.IntegrationTests.Fixtures;
using Content.Server._Floof.Persistence;
using Content.Server.Cloning;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared._Floof.Persistence;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Floof.Persistence;

/// <summary>
///     Reading live characters from in-round bodies and saving them with the console command, like a player would.
/// </summary>
[TestFixture]
[TestOf(typeof(LiveCharacterSystem))]
public sealed class LiveCharacterCommandTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true, DummyTicker = false };

    /// <summary>
    ///     Every species players can pick, straight from the prototypes.
    /// </summary>
    private List<SpeciesPrototype> PlayableSpecies()
    {
        return SProtoMan.EnumeratePrototypes<SpeciesPrototype>()
            .Where(s => s.RoundStart)
            .OrderBy(s => s.ID)
            .ToList();
    }

    /// <summary>
    ///     Sets the player's selected character to a seeded random profile of the species and respawns them as it.
    /// </summary>
    private async Task<Entity<LiveCharacterComponent>> SpawnAs(SpeciesPrototype species)
    {
        var session = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var prefs = Server.ResolveDependency<IServerPreferencesManager>();
        var ticker = Server.System<ServerGameTicker>();
        var system = Server.System<LiveCharacterSystem>();

        Assert.That(prefs.GetPreferences(session.UserId).SelectedCharacterIndex, Is.Zero);

        await Server.WaitPost(() =>
        {
            Server.ResolveDependency<IRobustRandom>().SetSeed(1);
            prefs.SetProfile(session.UserId, 0, HumanoidCharacterProfile.RandomWithSpecies(species.ID)).Wait();

            if (system.TryGetBody(session.UserId, 0, out var old))
                SEntMan.DeleteEntity(old);

            ticker.Respawn(session);
        });
        await Pair.RunTicksSync(5);

        Entity<LiveCharacterComponent> body = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(system.TryGetBody(session.UserId, 0, out body), $"{species.ID}: spawned body is not tracked");
            Assert.That(SEntMan.GetComponent<HumanoidProfileComponent>(body).Species, Is.EqualTo(new ProtoId<SpeciesPrototype>(species.ID)));
        });
        return body;
    }

    [Test]
    [Description("A freshly spawned body of every species reads back as the profile it was spawned from.")]
    public async Task BodyMatchesSpawnProfile()
    {
        var prefs = Server.ResolveDependency<IServerPreferencesManager>();
        var manager = Server.ResolveDependency<LiveCharacterManager>();
        var system = Server.System<LiveCharacterSystem>();
        var user = Server.ResolveDependency<IPlayerManager>().Sessions.Single().UserId;

        var diffs = new List<(string Species, string Body, string Profile)>();
        foreach (var species in PlayableSpecies())
        {
            var body = await SpawnAs(species);

            await Server.WaitAssertion(() =>
            {
                Assert.That(system.TryCreateRecord(body, out var record), $"{species.ID}: could not build a record from the body");

                var expected = LiveCharacterRecord.FromProfile(prefs.GetPreferences(user).SelectedCharacter);
                manager.Validate(expected, "profile");
                manager.Validate(record!, "body");
                IgnoreForcedColors(expected);
                IgnoreForcedColors(record!);

                diffs.Add((species.ID, manager.Serialize(record!), manager.Serialize(expected)));
            });
        }

        using (Assert.EnterMultipleScope())
        {
            foreach (var (species, body, profile) in diffs)
            {
                Assert.That(body, Is.EqualTo(profile), $"{species}: body differs from its profile");
            }
        }
    }

    /// <summary>
    ///     The body recolors forced and skin-matched markings and draws them last, whatever the profile picked,
    ///     so their colors and order can't be compared.
    /// </summary>
    private void IgnoreForcedColors(LiveCharacterRecord record)
    {
        var markingManager = Server.ResolveDependency<MarkingManager>();
        var groups = markingManager.GetMarkingData(record.Species);

        foreach (var (organ, layers) in record.Appearance.Markings)
        {
            var group = groups.TryGetValue(organ, out var data) ? SProtoMan.Index(data.Group) : null;
            foreach (var (layer, markings) in layers)
            {
                var matchSkin = group?.Appearances.GetValueOrDefault(layer)?.MatchSkin == true;
                var normalized = new List<(int Order, Marking Marking)>();
                foreach (var marking in markings)
                {
                    if (!markingManager.TryGetMarking(marking, out var proto))
                        continue;

                    // Same order the body draws them in: free colors, forced colors, then forced layers.
                    var order = matchSkin || proto.ForcedColoring ? 1 : proto.HasForcedColorLayer() ? 2 : 0;
                    var colors = marking.MarkingColors
                        .Select((c, j) => order == 1 || order == 2 && j < proto.Sprites.Count && proto.Sprites[j].ForcedColoring ? Color.White : c);
                    normalized.Add((order, new Marking(marking.MarkingId, colors) { Forced = marking.Forced }));
                }

                markings.Clear();
                markings.AddRange(normalized.OrderBy(m => m.Order).Select(m => m.Marking));
            }
        }
    }

    [Test]
    [Description("For every species the player saves with the command, swaps markings in round (scissors, mirror, admin tools), saves again, and both load back.")]
    public async Task InRoundMarkingChangesAreSaved()
    {
        var db = Server.ResolveDependency<IServerDbManager>();
        var manager = Server.ResolveDependency<LiveCharacterManager>();
        var serialization = Server.ResolveDependency<ISerializationManager>();
        var system = Server.System<LiveCharacterSystem>();
        var visualBody = Server.System<SharedVisualBodySystem>();
        var markingManager = Server.ResolveDependency<MarkingManager>();
        var protoMan = SProtoMan;
        var user = Server.ResolveDependency<IPlayerManager>().Sessions.Single().UserId;

        foreach (var species in PlayableSpecies())
        {
            var body = await SpawnAs(species);
            LiveCharacterRecord before = null;
            LiveCharacterRecord after = null;

            await Server.WaitAssertion(() => Assert.That(system.TryCreateRecord(body, out before)));

            await Pair.WaitClientCommand("livecharsave");
            await AssertLoadsAs(before!);

            // Swap every marking on the body for a different one with new colors, like scissors, the mirror or admin tools would.
            await Server.WaitAssertion(() =>
            {
                var swapped = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
                var organs = SEntMan.EntityQueryEnumerator<OrganComponent, VisualOrganMarkingsComponent>();
                while (organs.MoveNext(out var organ, out var visual))
                {
                    if (organ.Body != body.Owner || organ.Category is not { } category)
                        continue;

                    var group = protoMan.Index(visual.MarkingData.Group);
                    var current = before!.Appearance.Markings.GetValueOrDefault(category);
                    foreach (var layer in visual.MarkingData.Layers)
                    {
                        // Skin-matched layers and forced-color markings get their colors from the body.
                        if (group.Limits.TryGetValue(layer, out var limit) && limit.Limit < 1
                            || group.Appearances.GetValueOrDefault(layer)?.MatchSkin == true)
                            continue;

                        var old = current?.GetValueOrDefault(layer) ?? [];
                        var pick = markingManager.MarkingsByLayerAndGroupAndSex(layer, group.ID, before.Sex)
                            .OrderBy(m => m.Key)
                            .Select(m => m.Value)
                            .FirstOrDefault(m => !m.ForcedColoring && !m.HasForcedColorLayer() && old.All(o => o.MarkingId != m.ID));
                        if (pick == null)
                            continue;

                        swapped.GetOrNew(category)[layer] = [new Marking(pick.ID, pick.Sprites.Select(_ => Color.Red))];
                    }
                }

                Assert.That(swapped, Is.Not.Empty, $"{species.ID}: no markings to swap");
                visualBody.ApplyMarkings(body, swapped);

                Assert.That(system.TryCreateRecord(body, out after));
                using (Assert.EnterMultipleScope())
                {
                    foreach (var (organ, layers) in swapped)
                    {
                        foreach (var (layer, markings) in layers)
                        {
                            var read = after!.Appearance.Markings.GetValueOrDefault(organ)?.GetValueOrDefault(layer);
                            Assert.That(read, Is.EqualTo(markings), $"{species.ID}: swap on {organ}/{layer} was not read from the body");
                        }
                    }
                }
            });

            await Pair.WaitClientCommand("livecharsave");
            await AssertLoadsAs(after!);

            async Task AssertLoadsAs(LiveCharacterRecord expected)
            {
                var live = await db.GetLiveCharacterAsync(user, body.Comp.Slot);
                Assert.That(live, Is.Not.Null, $"{species.ID}: command did not save a live character");

                await Server.WaitAssertion(() =>
                {
                    var loaded = serialization.Read<LiveCharacterRecord>(LiveCharacterManager.Parse(live!.Data), notNullableOverride: true);
                    manager.Validate(loaded, "loaded");
                    manager.Validate(expected, "expected");
                    Assert.That(manager.Serialize(loaded), Is.EqualTo(manager.Serialize(expected)), $"{species.ID}: loaded record differs from the saved one");
                });
            }
        }
    }

    [Test]
    [Description("A character can't spawn again while its body is still in the round, and can once the body is gone.")]
    public async Task NoSecondSpawnWhileBodyInRound()
    {
        var session = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var ticker = Server.System<ServerGameTicker>();
        var system = Server.System<LiveCharacterSystem>();
        var clientStatus = Client.System<LiveCharacterStatusSystem>();
        var body = await SpawnAs(PlayableSpecies().First());

        await Server.WaitPost(() => ticker.Respawn(session));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Count<LiveCharacterComponent>(), Is.EqualTo(1), "Character spawned a second time");
            Assert.That(session.AttachedEntity, Is.Not.EqualTo(body.Owner));
            Assert.That(SEntMan.HasComponent<LiveCharacterComponent>(session.AttachedEntity), Is.False, "Player was put in a character body");
        });
        await Client.WaitAssertion(() =>
            Assert.That(clientStatus.SelectedInRound(), "Lobby does not know the character is in the round"));

        await Server.WaitPost(() => SEntMan.DeleteEntity(body));
        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
            Assert.That(clientStatus.SelectedInRound(), Is.False, "Lobby still blocks the character after its body is gone"));

        await Server.WaitPost(() => ticker.Respawn(session));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(system.TryGetBody(session.UserId, 0, out var newBody), "Character could not spawn after its body was gone");
            Assert.That(session.AttachedEntity, Is.EqualTo(newBody.Owner));
        });
    }

    [Test]
    [Description("A clone becomes the character only when it comes from a cloning pod and the character's own player enters it.")]
    public async Task CloneTakesOverCharacter()
    {
        var session = Server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var cloning = Server.System<CloningSystem>();
        var mindSystem = Server.System<SharedMindSystem>();
        var system = Server.System<LiveCharacterSystem>();
        var body = await SpawnAs(PlayableSpecies().First());
        var otherPlayer = await Server.AddDummySession();
        EntityUid? stranger = null;
        EntityUid? paradox = null;
        EntityUid? own = null;

        // Neither another player in a pod clone nor the player in a non-pod clone takes the character.
        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(body);
            Assert.That(cloning.TryCloning(body, coords, "CloningPod", out stranger));
            Assert.That(cloning.TryCloning(body, coords, "ParadoxCloningSettings", out paradox));

            var otherMind = mindSystem.CreateMind(otherPlayer.UserId);
            mindSystem.TransferTo(otherMind, stranger);

            mindSystem.TryGetMind(session, out var mind, out _);
            mindSystem.TransferTo(mind, paradox);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(system.TryGetBody(session.UserId, 0, out var current));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(current.Owner, Is.EqualTo(body.Owner), "Another player or a paradox clone took the character");
                Assert.That(SEntMan.Count<LiveCharacterComponent>(), Is.EqualTo(1));
            }
        });

        // The player revived in their own pod clone takes it.
        await Server.WaitAssertion(() =>
        {
            var coords = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(body);
            Assert.That(cloning.TryCloning(body, coords, "CloningPod", out own));

            mindSystem.TryGetMind(session, out var mind, out _);
            mindSystem.TransferTo(mind, own);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(system.TryGetBody(session.UserId, 0, out var current));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(current.Owner, Is.EqualTo(own), "Character did not move to the player's clone");
                Assert.That(SEntMan.Count<LiveCharacterComponent>(), Is.EqualTo(1), "Old body or other clone still holds the character");
                Assert.That(system.TryGetOwnBody(session, out var ownBody) && ownBody.Owner == own, "Save command would not find the clone");
            }
        });
    }
}
