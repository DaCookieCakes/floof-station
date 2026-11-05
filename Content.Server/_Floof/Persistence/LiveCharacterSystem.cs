using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Station.Systems;
using Content.Shared._Floof.Persistence;
using Content.Shared.Body;
using Content.Shared.Cloning;
using Content.Shared.Cloning.Events;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.Persistence;

/// <summary>
///     Tracks which character slot a body belongs to and builds live character records from bodies.
/// </summary>
public sealed partial class LiveCharacterSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private LiveCharacterManager _liveCharacters = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>
    ///     The only cloning that can take over a character: reviving a dead player.
    /// </summary>
    private static readonly ProtoId<CloningSettingsPrototype> RevivalCloning = "CloningPod";

    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly ProtoId<OrganCategoryPrototype> Dick = "Dick";
    private static readonly ProtoId<OrganCategoryPrototype> Vagina = "Vagina";
    private static readonly ProtoId<OrganCategoryPrototype> Breasts = "Breasts";

    /// <summary>
    ///     Each connected player's live characters, keyed by slot. Spawning can't wait on the database.
    /// </summary>
    private readonly Dictionary<NetUserId, Dictionary<int, LiveCharacterInfo>> _live = new();

    /// <summary>
    ///     Every character that spawned this round. Whichever doesn't make it to CC loses its live version.
    /// </summary>
    private readonly HashSet<(NetUserId User, int Slot)> _spawned = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<LiveCharacterComponent, CloningEvent>(OnCloned);
        SubscribeLocalEvent<LiveCharacterCloneComponent, MindAddedMessage>(OnCloneMindAdded);
        SubscribeLocalEvent<LiveCharacterComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<LiveCharacterDiscardRequestEvent>(OnDiscardRequest);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _spawned.Clear());

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _live.Remove(args.Session.UserId);

        if (args.NewStatus != SessionStatus.InGame)
            return;

        SendInRound(args.Session.UserId);
        SendLive(args.Session);
    }

    private async void OnDiscardRequest(LiveCharacterDiscardRequestEvent ev, EntitySessionEventArgs args)
    {
        // The body would just be saved again at round end.
        if (!TryGetBody(args.SenderSession.UserId, ev.Slot, out _))
            await _liveCharacters.Discard(args.SenderSession.UserId, ev.Slot);

        SendLive(args.SenderSession);
    }

    /// <summary>
    ///     Reloads the player's live characters and sends them to the player. Call after changing them in the database.
    /// </summary>
    public async void SendLive(ICommonSession session)
    {
        var characters = await _liveCharacters.LoadAll(session.UserId);
        if (session.Status == SessionStatus.Disconnected)
            return;

        _live[session.UserId] = characters;
        RaiseNetworkEvent(new LiveCharactersEvent(characters), session);
    }

    /// <summary>
    ///     Forgets a deleted slot's live character, which the database deletes along with the slot.
    /// </summary>
    public void Forget(NetUserId user, int slot)
    {
        if (_live.TryGetValue(user, out var characters))
            characters.Remove(slot);
    }

    /// <summary>
    ///     Builds the profile to spawn with: the slot's live character if it has one, otherwise the template.
    /// </summary>
    public HumanoidCharacterProfile GetSpawnProfile(NetUserId user, int slot, HumanoidCharacterProfile template)
    {
        if (!_live.TryGetValue(user, out var characters))
        {
            Log.Warning($"Live characters of {user} weren't loaded yet, spawning the template of slot {slot}");
            return template;
        }

        return characters.TryGetValue(slot, out var info) ? info.Record.ApplyTo(template) : template;
    }

    private void OnShutdown(Entity<LiveCharacterComponent> ent, ref ComponentShutdown args)
    {
        SendInRound(ent.Comp.User, ent);
    }

    /// <summary>
    ///     Sends the player which of their slots have a body in the round.
    /// </summary>
    private void SendInRound(NetUserId user, EntityUid? except = null)
    {
        if (!_player.TryGetSessionById(user, out var session))
            return;

        var slots = new HashSet<int>();
        var query = EntityQueryEnumerator<LiveCharacterComponent>();
        while (query.MoveNext(out var uid, out var live))
        {
            if (live.User == user && uid != except)
                slots.Add(live.Slot);
        }

        RaiseNetworkEvent(new LiveCharacterInRoundEvent(slots), session);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Bodies not spawned from the selected slot (e.g. random characters for outside antags) are never saved.
        if (!_prefs.TryGetCachedPreferences(args.Player.UserId, out var prefs)
            || !GetSpawnProfile(args.Player.UserId, prefs.SelectedCharacterIndex, prefs.SelectedCharacter).MemberwiseEquals(args.Profile))
            return;

        Track(args.Mob, args.Player.UserId, prefs.SelectedCharacterIndex);
    }

    /// <summary>
    ///     Makes the body the character of a player's slot, taking it over from any other body of that slot.
    /// </summary>
    public void Track(EntityUid body, NetUserId user, int slot)
    {
        if (TryGetBody(user, slot, out var old) && old.Owner != body)
            RemComp<LiveCharacterComponent>(old);

        var live = EnsureComp<LiveCharacterComponent>(body);
        live.User = user;
        live.Slot = slot;
        _spawned.Add((user, slot));
        SendInRound(user);
        RaiseTracked(body);
    }

    /// <summary>
    ///     Whether the player's selected slot has a live character.
    /// </summary>
    public bool HasLive(NetUserId user)
    {
        return _prefs.TryGetCachedPreferences(user, out var prefs)
            && _live.TryGetValue(user, out var characters)
            && characters.ContainsKey(prefs.SelectedCharacterIndex);
    }

    /// <summary>
    ///     Spawns the player's selected character at the coordinates and moves their mind into it. A tracked body
    ///     becomes the character, saved at round end; an untracked one is just a copy.
    /// </summary>
    public EntityUid? SpawnHere(ICommonSession player, EntityCoordinates coords, EntityUid? station, bool live, bool tracked)
    {
        if (!_prefs.TryGetCachedPreferences(player.UserId, out var prefs))
            return null;

        var slot = prefs.SelectedCharacterIndex;
        var profile = prefs.SelectedCharacter;
        if (live)
        {
            if (!_live.TryGetValue(player.UserId, out var characters) || !characters.TryGetValue(slot, out var info))
                return null;

            profile = info.Record.ApplyTo(profile);
        }

        var mob = _spawning.SpawnPlayerMob(coords, null, profile, station);
        if (_mind.TryGetMind(player, out var mindId, out var mind))
            _mind.TransferTo(mindId, mob, true, mind: mind);

        // After the mind moved in, so the bank account can go on it.
        if (tracked)
            Track(mob, player.UserId, slot);

        return mob;
    }

    /// <summary>
    ///     Saves every character that escaped to CC and discards the live version of every other one that spawned.
    /// </summary>
    private void OnRoundEnd(ref RoundEndTextAppendEvent args)
    {
        var escaped = new Dictionary<(NetUserId User, int Slot), LiveCharacterRecord>();
        var query = EntityQueryEnumerator<LiveCharacterComponent>();
        while (query.MoveNext(out var uid, out var live))
        {
            if (!IsEscaped(uid))
                continue;

            if (TryCreateRecord(uid, out var record))
                escaped[(live.User, live.Slot)] = record;
            else
                Log.Error($"Failed to create a live character record of {ToPrettyString(uid)}, slot {live.Slot} of {live.User}");
        }

        var roundId = _ticker.RoundId;
        foreach (var character in _spawned)
        {
            if (escaped.TryGetValue(character, out var record))
                SaveCharacter(character.User, character.Slot, record, roundId);
            else
                DiscardCharacter(character.User, character.Slot);
        }

        _spawned.Clear();
    }

    /// <summary>
    ///     Saves a character leaving the round early through cryo, or discards its live version if the body is dead.
    ///     The body stops being the character either way, so the slot can join again this round.
    /// </summary>
    public void LeaveRound(EntityUid body)
    {
        if (!TryComp<LiveCharacterComponent>(body, out var live))
            return;

        var user = live.User;
        var slot = live.Slot;
        _spawned.Remove((user, slot));
        RemComp<LiveCharacterComponent>(body);

        if (!_mobState.IsDead(body) && TryCreateRecord(body, out var record))
            SaveCharacter(user, slot, record, _ticker.RoundId);
        else
            DiscardCharacter(user, slot);
    }

    /// <summary>
    ///     Whether a body made it to CC: on the evac shuttle after it left the station, or on CC itself.
    ///     Being inside something (body bag, locker, mech) counts, as do crit, dead, cuffed and SSD.
    /// </summary>
    private bool IsEscaped(EntityUid body)
    {
        var xform = Transform(body);
        if (_emergencyShuttle.ShuttlesLeft && _emergencyShuttle.IsTargetEscaping(body))
            return true;

        var query = EntityQueryEnumerator<StationCentcommComponent>();
        while (query.MoveNext(out var centcomm))
        {
            if (centcomm.MapEntity != null && xform.MapUid == centcomm.MapEntity)
                return true;
        }

        return false;
    }

    private async void SaveCharacter(NetUserId user, int slot, LiveCharacterRecord record, int roundId)
    {
        if (!await _liveCharacters.Save(user, slot, record, roundId))
            Log.Error($"Failed to save the live character of slot {slot} of {user}");

        if (_player.TryGetSessionById(user, out var session))
            SendLive(session);
    }

    private async void DiscardCharacter(NetUserId user, int slot)
    {
        await _liveCharacters.Discard(user, slot);

        if (_player.TryGetSessionById(user, out var session))
            SendLive(session);
    }

    /// <summary>
    ///     Remembers which body a pod clone came from. Other cloning (paradox clones, changeling identities,
    ///     admin clones) never takes over the character.
    /// </summary>
    private void OnCloned(Entity<LiveCharacterComponent> ent, ref CloningEvent args)
    {
        if (args.Settings.ID != RevivalCloning)
            return;

        EnsureComp<LiveCharacterCloneComponent>(args.CloneUid).Source = ent;

        // Cloning copies the organ sprites but not their profiles, which records take the colors from.
        if (_visualBody.TryGatherMarkingsData(ent.Owner, null, out var profiles, out _, out _))
            _visualBody.ApplyProfiles(args.CloneUid, profiles);
    }

    /// <summary>
    ///     The character's own player entered the clone, so the clone is the character now.
    /// </summary>
    private void OnCloneMindAdded(EntityUid uid, LiveCharacterCloneComponent comp, MindAddedMessage args)
    {
        var source = comp.Source;
        RemCompDeferred<LiveCharacterCloneComponent>(uid);

        if (!TryComp<LiveCharacterComponent>(source, out var original) || args.Mind.Comp.UserId != original.User)
            return;

        var live = EnsureComp<LiveCharacterComponent>(uid);
        live.User = original.User;
        live.Slot = original.Slot;
        RemComp<LiveCharacterComponent>(source);
        SendInRound(live.User);
        RaiseTracked(uid);
    }

    private void RaiseTracked(EntityUid body)
    {
        var ev = new LiveCharacterTrackedEvent();
        RaiseLocalEvent(body, ref ev);
    }

    /// <summary>
    ///     Finds the body of a player's character slot, wherever it is. There is at most one, since a character
    ///     can't spawn while its body is still in the round.
    /// </summary>
    public bool TryGetBody(NetUserId user, int slot, out Entity<LiveCharacterComponent> body)
    {
        var query = EntityQueryEnumerator<LiveCharacterComponent>();
        while (query.MoveNext(out var uid, out var live))
        {
            if (live.User != user || live.Slot != slot)
                continue;

            body = (uid, live);
            return true;
        }

        body = default;
        return false;
    }

    /// <summary>
    ///     Finds the character body the player's mind belongs to, including while ghosted out of it.
    /// </summary>
    public bool TryGetOwnBody(ICommonSession player, out Entity<LiveCharacterComponent> body)
    {
        body = default;
        if (!_mind.TryGetMind(player, out _, out var mind)
            || !TryComp<LiveCharacterComponent>(mind.OwnedEntity, out var live)
            || live.User != player.UserId)
            return false;

        body = (mind.OwnedEntity.Value, live);
        return true;
    }

    /// <summary>
    ///     Builds a live character record from the current state of a body.
    /// </summary>
    public bool TryCreateRecord(EntityUid body, [NotNullWhen(true)] out LiveCharacterRecord? record)
    {
        record = null;
        if (!TryComp<HumanoidProfileComponent>(body, out var humanoid)
            || !_visualBody.TryGatherMarkingsData(body, null, out var profiles, out _, out var markings))
            return false;

        // Every organ gets the same colors at spawn, so any one of them holds the body's colors.
        var colors = profiles.GetValueOrDefault(Torso, profiles.Values.FirstOrDefault());

        record = new LiveCharacterRecord
        {
            Name = Name(body),
            Species = humanoid.Species,
            Age = humanoid.Age,
            Sex = humanoid.Sex,
            Voice = humanoid.Voice,
            Gender = humanoid.Gender,
            Height = humanoid.Height,
            Width = humanoid.Width,
            Appearance = new HumanoidCharacterAppearance(
                colors.EyeColor,
                colors.SkinColor,
                CopyMarkings(markings)),
            Genitals = new Genitals
            {
                Penis = profiles.ContainsKey(Dick),
                Vagina = profiles.ContainsKey(Vagina),
                Breasts = profiles.ContainsKey(Breasts),
            },
        };
        return true;
    }

    /// <summary>
    ///     Copies the body's markings, leaving out empty entries like profiles do.
    /// </summary>
    private static Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> CopyMarkings(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        var copy = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
        foreach (var (organ, layers) in markings)
        {
            var organCopy = layers
                .Where(l => l.Value.Count > 0)
                .ToDictionary(l => l.Key, l => l.Value.ToList());

            if (organCopy.Count > 0)
                copy[organ] = organCopy;
        }

        return copy;
    }
}
