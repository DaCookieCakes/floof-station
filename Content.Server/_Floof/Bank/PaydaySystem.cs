using Content.Server._Floof.Persistence;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared._Floof.Bank;
using Content.Shared._Floof.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Bank;

/// <summary>
/// Pays every character at the same time each payday. Alive checks run in between;
/// checks spent dead, ghosted, SSD (or not spawned yet) reduce the pay to half or nothing.
/// </summary>
public sealed partial class PaydaySystem : EntitySystem
{
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private BankPdaSystem _bankPda = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private LiveCharacterSystem _liveCharacter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StationRecordsSystem _stationRecords = default!;

    private readonly Dictionary<(NetUserId User, int Slot), PaydayRecord> _records = new();
    private TimeSpan? _nextCheck;
    private int _checks;
    private int _roundChecks;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    /// <summary>
    /// Full pay if dead for less than half of the checks, nothing if dead for all of them, half otherwise.
    /// </summary>
    public static int CalculatePay(int pay, int aliveChecks, int checks)
    {
        if ((checks - aliveChecks) * 2 < checks)
            return pay;

        return aliveChecks > 0 ? pay / 2 : 0;
    }

    public override void Update(float frameTime)
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        var interval = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.BankAliveCheckInterval));
        _nextCheck ??= _timing.CurTime + interval;
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck += interval;
        _checks++;
        _roundChecks++;

        // Only the minds of characters persistence tracks have accounts, so ghost roles and other mobs never count.
        var query = EntityQueryEnumerator<BankAccountComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var account, out var mind))
        {
            var key = (account.UserId, account.Slot);
            if (!_records.TryGetValue(key, out var record))
            {
                _records[key] = record = new PaydayRecord(_jobs.MindTryGetJobId(mindId, out var job) ? job : null);
                // The PDA is issued when the character spawns, so one lookup is enough.
                if (_bankPda.TryGetPda(account.UserId, account.Slot, out var pda))
                    record.Pda = pda.Owner;
            }

            UpdateJob(record);
            if (!IsPresent(account, mind))
                continue;

            record.AliveChecks++;
            record.RoundAliveChecks++;
        }

        if (_checks < _cfg.GetCVar(CCVars.BankChecksPerPayday))
            return;

        foreach (var (key, record) in _records)
        {
            var pay = GetPay(record);
            var amount = CalculatePay(pay, record.AliveChecks, _checks);
            var message = amount == pay ? "bank-payday" : "bank-payday-reduced";
            record.AliveChecks = 0;
            Pay(key.User, key.Slot, amount, message);
        }

        _checks = 0;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PostRound)
            return;

        foreach (var (key, record) in _records)
        {
            if (!_liveCharacter.TryGetBody(key.User, key.Slot, out var body)
                || !HasComp<MobStateComponent>(body)
                || !_emergency.IsTargetEscaping(body))
                continue;

            // Same full/half/nothing rule as paydays, over the whole round.
            var pay = GetPay(record) * 2;
            var amount = CalculatePay(pay, record.RoundAliveChecks, _roundChecks);
            Pay(key.User, key.Slot, amount, amount == pay ? "bank-cc-bonus" : "bank-cc-bonus-reduced");
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _records.Clear();
        _nextCheck = null;
        _checks = 0;
        _roundChecks = 0;
    }

    /// <summary>
    /// The character's own player has to be in the character's body (or borg): ghosts (aghosts included),
    /// ghost roles and SSD count as dead.
    /// </summary>
    private bool IsPresent(BankAccountComponent account, MindComponent mind)
    {
        return mind.UserId == account.UserId
            && mind.OwnedEntity is { } body
            && TryComp<ActorComponent>(body, out var actor)
            && actor.PlayerSession.UserId == account.UserId
            && HasComp<MobStateComponent>(body)
            && !_mobState.IsDead(body);
    }

    /// <summary>
    /// Follows job changes made at the ID console, which update the character's station record.
    /// </summary>
    private void UpdateJob(PaydayRecord record)
    {
        if (record.RecordKey == null
            && TryComp<PdaComponent>(record.Pda, out var pda)
            && TryComp<StationRecordKeyStorageComponent>(pda.ContainedId, out var storage))
            record.RecordKey = storage.Key;

        if (record.RecordKey is { } key
            && _stationRecords.TryGetRecord<GeneralStationRecord>(key, out var general)
            && !string.IsNullOrEmpty(general.JobPrototype))
            record.Job = general.JobPrototype;
    }

    private int GetPay(PaydayRecord record)
    {
        return record.Job is { } job && _proto.TryIndex<JobPrototype>(job, out var proto) ? proto.Pay : 0;
    }

    private async void Pay(NetUserId userId, int slot, int amount, LocId message)
    {
        if (amount > 0)
            await _bank.Deposit(userId, slot, amount, message, message);
    }

    private sealed class PaydayRecord(string? job)
    {
        public string? Job = job;
        public EntityUid? Pda;
        public StationRecordKey? RecordKey;
        public int AliveChecks;
        public int RoundAliveChecks;
    }
}
