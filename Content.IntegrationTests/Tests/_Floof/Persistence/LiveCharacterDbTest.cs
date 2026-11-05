using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Database;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Floof.Persistence;

/// <summary>
///     Saving, loading, backing up and pruning live characters in the database.
/// </summary>
[TestFixture]
[TestOf(typeof(ServerDbBase))]
public sealed class LiveCharacterDbTest : GameTest
{
    private const int HistoryMin = 7;
    private const int HistoryDays = 30;

    private static readonly NetUserId User = new(new Guid("5b0a1f9e-3c2d-4e8a-9f61-2a7c4d8e0b13"));

    private static (ServerDbSqlite Db, DbContextOptions<SqliteServerDbContext> Options) GetDb(
        RobustIntegrationTest.ServerIntegrationInstance server)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var serialization = server.ResolveDependency<ISerializationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        var db = new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog, serialization, false);
        return (db, builder.Options);
    }

    private static Task<bool> Save(ServerDbSqlite db, string data, int slot = 0)
    {
        return db.SaveLiveCharacterAsync(User, slot, 1, data, null, LiveCharacterHistoryReason.RoundEnd, HistoryMin, HistoryDays);
    }

    private static List<LiveCharacterHistory> History(DbContextOptions<SqliteServerDbContext> options)
    {
        using var ctx = new SqliteServerDbContext(options);
        return ctx.LiveCharacterHistory.OrderBy(h => h.SavedAt).ToList();
    }

    [Test]
    public async Task OverwriteAndDiscardKeepBackups()
    {
        var (db, options) = GetDb(Server);
        await db.InitPrefsAsync(User, new HumanoidCharacterProfile());

        await Save(db, "a");
        Assert.That(History(options), Is.Empty, "First save made a backup");

        await Save(db, "b");
        Assert.That(await db.DeleteLiveCharacterAsync(User, 0, HistoryMin, HistoryDays));

        var history = History(options);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.GetLiveCharacterAsync(User, 0), Is.Null);
            Assert.That(history.Select(h => (h.Data, h.Reason)), Is.EqualTo(new[]
            {
                ("a", LiveCharacterHistoryReason.RoundEnd),
                ("b", LiveCharacterHistoryReason.Discard),
            }));
        }
    }

    [Test]
    public async Task PruneKeepsRecentAndMinimum()
    {
        var (db, options) = GetDb(Server);
        await db.InitPrefsAsync(User, new HumanoidCharacterProfile());

        // 10 saves leave 9 backups, all recent, so none are pruned despite exceeding the minimum.
        for (var i = 0; i < 10; i++)
        {
            await Save(db, $"v{i}");
        }

        Assert.That(History(options), Has.Count.EqualTo(9), "Recent backups were pruned");

        // Age every backup past the cutoff, keeping their order.
        await using (var ctx = new SqliteServerDbContext(options))
        {
            var old = DateTime.UtcNow.AddDays(-HistoryDays * 2);
            foreach (var h in ctx.LiveCharacterHistory)
            {
                h.SavedAt = old.AddMinutes(h.Id);
            }

            await ctx.SaveChangesAsync();
        }

        await Save(db, "v10");

        // Only the newest backups up to the minimum survive: v3..v8 old, plus v9 just backed up.
        Assert.That(History(options).Select(h => h.Data), Is.EqualTo(new[] { "v3", "v4", "v5", "v6", "v7", "v8", "v9" }));
    }

    [Test]
    public async Task DeletingSlotDeletesLiveCharacter()
    {
        var (db, options) = GetDb(Server);
        await db.InitPrefsAsync(User, new HumanoidCharacterProfile());
        await db.SaveCharacterSlotAsync(User, new HumanoidCharacterProfile(), 1);

        await Save(db, "a", slot: 1);
        await Save(db, "b", slot: 1);
        await db.SaveCharacterSlotAsync(User, null, 1);

        await using var ctx = new SqliteServerDbContext(options);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ctx.LiveCharacter.CountAsync(), Is.Zero, "Live character outlived its slot");
            Assert.That(await ctx.LiveCharacterHistory.CountAsync(), Is.Zero, "History outlived its slot");
        }
    }
}
