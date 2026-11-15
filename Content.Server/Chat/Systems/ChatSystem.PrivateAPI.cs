using System.Globalization;
using System.Linq;
using Content.Server._Starlight.Language;
using Content.Shared._Starlight.Language;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Radio;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    private void SendEntitySpeak(
        EntityUid source,
        string originalMessage,
        ChatTransmitRange range,
        string? nameOverride,
        LanguagePrototype language,
        bool hideLog = false,
        bool ignoreActionBlocker = false
        )
    {
        if (!_actionBlocker.CanSpeak(source) && !ignoreActionBlocker)
            return;

        var message = TransformSpeech(source, originalMessage, language);

        if (message.Length == 0)
            return;

        var speech = GetSpeechVerb(source, message);

        // get the entity's apparent name (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.VoiceName;
            // Check for a speech verb override
            if (nameEv.SpeechVerb != null && ProtoMan.Resolve(nameEv.SpeechVerb, out var proto))
                speech = proto;
        }

        name = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(name)}\" entity=\"{GetNetEntity(source)}\" entitynamecolor=\"true\"]" : FormattedMessage.EscapeText(name);
        var wrappedMessage = WrapPublicMessage(source, name, message, language: language);

        var obfuscated = SanitizeInGameICMessage(source, _language.ObfuscateSpeech(message, language), out var emoteStr, true, _configurationManager.GetCVar(CCVars.ChatPunctuation),
            (!CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Parent.Name == "en")
            || (CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Name == "en"));
        // The language-obfuscated message wrapped in a "x says y" string.
        var wrappedObfuscated = WrapPublicMessage(source, name, obfuscated, language: language, obfuscated: true);

        SendInVoiceRange(ChatChannel.Local, name, message,wrappedMessage,obfuscated, wrappedObfuscated, source, range, languageOverride: language);

        var ev = new EntitySpokeEvent(source, message, null, false, language);
        RaiseLocalEvent(source, ev, true);

        // To avoid logging any messages sent by entities that are not players, like vendors, cloning, etc.
        // Also doesn't log if hideLog is true.
        if (!HasComp<ActorComponent>(source) || hideLog)
            return;

        if (originalMessage == message)
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {source} as {name}: {originalMessage}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {source}: {originalMessage}.");
        }
        else
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {source} as {name}, original: {originalMessage}, transformed: {message}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {source}, original: {originalMessage}, transformed: {message}.");
        }
    }

    private void SendEntityWhisper(
        EntityUid source,
        string originalMessage,
        ChatTransmitRange range,
        RadioChannelPrototype? channel,
        string? nameOverride,
        LanguagePrototype language,
        bool hideLog = false,
        bool ignoreActionBlocker = false
        )
    {
        if (!_actionBlocker.CanSpeak(source) && !ignoreActionBlocker)
            return;

        var message = TransformSpeech(source, FormattedMessage.RemoveMarkupOrThrow(originalMessage), language);
        if (message.Length == 0)
            return;

        var obfuscatedMessage = ObfuscateMessageReadability(message, 0.2f);

        // get the entity's name by visual identity (if no override provided).
        string nameIdentity = FormattedMessage.EscapeText(nameOverride ?? Identity.Name(source, EntityManager));
        // get the entity's name by voice (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.VoiceName;
        }
        name = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(name)}\" entity=\"{GetNetEntity(source)}\" entitynamecolor=\"true\"]" : FormattedMessage.EscapeText(name);


        var languageObfuscatedMessage = SanitizeInGameICMessage(source, _language.ObfuscateSpeech(message, language), out var emoteStr, true, _configurationManager.GetCVar(CCVars.ChatPunctuation),
            (!CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Parent.Name == "en")
            || (CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Name == "en")); // Starlight

        foreach (var (session, data) in GetRecipients(source, WhisperClearRange, ghostsCanHear: false))
        {
            if (session.AttachedEntity is not { Valid: true } listener) // Starlight-edit: Languages
                continue;

            if (MessageRangeCheck(session, data, range) != MessageRangeCheckResult.Full)
                continue; // Won't get logged to chat, and ghosts are too far away to see the pop-up, so we just won't send it to them.


            // Starlight - Start
            var canUnderstandLanguage = _language.CanUnderstand(listener, language.ID);
            // How the entity perceives the message depends on whether it can understand its language
            var perceivedMessage = canUnderstandLanguage ? message : languageObfuscatedMessage;
            var obfuscated = canUnderstandLanguage != true;

            // Result is the intermediate message derived from the perceived one via obfuscation
            // Wrapped message is the result wrapped in an "x says y" string
            string result, wrappedMessage;
            if (data.Range <= WhisperClearRange || data.Observer)
            {
                // Scenario 1: the listener can clearly understand the message
                result = perceivedMessage;
                wrappedMessage = WrapWhisperMessage(source, "chat-manager-entity-whisper-wrap-message", name, result, language, obfuscated);
            }
            else if (_examineSystem.InRangeUnOccluded(source, listener, WhisperMuffledRange))
            {
                // Scenario 2: if the listener is too far, they only hear fragments of the message
                result = ObfuscateMessageReadability(perceivedMessage, 0.2f);
                wrappedMessage = WrapWhisperMessage(source, "chat-manager-entity-whisper-wrap-message", nameIdentity, result, language, obfuscated);
            }
            else
            {
                // Scenario 3: If listener is too far and has no line of sight, they can't identify the whisperer's identity
                result = ObfuscateMessageReadability(perceivedMessage, 0.2f);
                wrappedMessage = WrapWhisperMessage(source, "chat-manager-entity-whisper-unknown-wrap-message", string.Empty, result, language, obfuscated);
            }

            _chatManager.ChatMessageToOne(ChatChannel.Whisper, result, wrappedMessage, source, false, session.Channel);
            // Starlight - End
        }

        var replayWrap = WrapWhisperMessage(source, "chat-manager-entity-whisper-wrap-message", name, message, language); // Starlight-edit: Languages
        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Whisper, message, replayWrap, GetNetEntity(source), null, MessageRangeHideChatForReplay(range))); // Starlight-edit: Languages

        var ev = new EntitySpokeEvent(source, message, channel, obfuscatedMessage, true, language );
        RaiseLocalEvent(source, ev, true);
        if (!hideLog)
            if (originalMessage == message)
            {
                if (name != Name(source))
                    _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {source} as {name}: {originalMessage}.");
                else
                    _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {source}: {originalMessage}.");
            }
            else
            {
                if (name != Name(source))
                    _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {source} as {name}, original: {originalMessage}, transformed: {message}.");
                else
                    _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {source}, original: {originalMessage}, transformed: {message}.");
            }
    }

    protected override void SendEntityEmote(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        LanguagePrototype language,
        bool hideLog = false,
        bool checkEmote = true,
        bool ignoreActionBlocker = false,
        NetUserId? author = null
        )
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        // get the entity's apparent name (if no override provided).
        var ent = Identity.Entity(source, EntityManager);
        string name = FormattedMessage.EscapeText(nameOverride ?? Name(ent));

        // Emotes use Identity.Name, since it doesn't actually involve your voice at all.
        var wrappedMessage = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", name),
            ("entity", ent),
            ("message", FormattedMessage.RemoveMarkupOrThrow(action)));

        if (checkEmote &&
            !TryEmoteChatInput(source, action))
            return;

        SendInVoiceRange(ChatChannel.Emotes, name, action, wrappedMessage, obfuscated: "", obfuscatedWrappedMessage: "", source, range, author); // Starlight
        if (!hideLog)
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {source} as {name}: {action}");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {source}: {action}");
    }

    // ReSharper disable once InconsistentNaming
    private void SendLOOC(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        var name = FormattedMessage.EscapeText(Identity.Name(source, EntityManager));
        name = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(name)}\" entity=\"{GetNetEntity(source)}\" color=\"{ChatChannel.LOOC.TextColor().ToHex()}\"]": FormattedMessage.EscapeText(name);
        if (_adminManager.IsAdmin(player))
        {
            if (!_adminLoocEnabled) return;
        }
        else if (!_loocEnabled) return;

        // If crit player LOOC is disabled, don't send the message at all.
        if (!_critLoocEnabled && _mobStateSystem.IsCritical(source))
            return;

        var wrappedMessage = Loc.GetString("chat-manager-entity-looc-wrap-message",
            ("entityName", name),
            ("message", FormattedMessage.EscapeText(message)));

        SendInVoiceRange(ChatChannel.LOOC, name, message, wrappedMessage,
            obfuscated: string.Empty,
            obfuscatedWrappedMessage: string.Empty, // will be skipped anyway
            source,
            hideChat ? ChatTransmitRange.HideChat : ChatTransmitRange.Normal,
            player.UserId,
            languageOverride: LanguageSystem.Universal); // Starlight

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"LOOC from {player:Player}: {message}");
    }

    // Floof - Start
    private void SendEntitySubtle(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        NetUserId? author = null
    )
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        // get the entity's apparent name (if no override provided).
        var ent = Identity.Entity(source, EntityManager);
        string name = FormattedMessage.EscapeText(nameOverride ?? Name(ent));

        // Emotes use Identity.Name, since it doesn't actually involve your voice at all.
        var wrappedMessage = Loc.GetString("chat-manager-entity-subtle-wrap-message",
            ("entityName", name),
            ("entity", ent),
            ("message", FormattedMessage.RemoveMarkup(action)));

        foreach (var (session, data) in GetRecipients(source, WhisperClearRange, ghostsCanHear: false))
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            if (MessageRangeCheck(session, data, range) == MessageRangeCheckResult.Disallowed)
                continue;

            _chatManager.ChatMessageToOne(ChatChannel.Emotes, action, wrappedMessage, source, false, session.Channel);
        }

        if (!hideLog)
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Subtle from {ToPrettyString(source):user} as {name}: {action}");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Subtle from {ToPrettyString(source):user}: {action}");
    }

    private void SendEntitySubtleOOC(
        EntityUid source,
        string action,
        ChatTransmitRange range,
        string? nameOverride,
        bool hideLog = false,
        bool ignoreActionBlocker = false,
        NetUserId? author = null
    )
    {
        if (!_actionBlocker.CanEmote(source) && !ignoreActionBlocker)
            return;

        // get the entity's apparent name (if no override provided).
        var ent = Identity.Entity(source, EntityManager);
        string name = FormattedMessage.EscapeText(nameOverride ?? Name(ent));

        // Emotes use Identity.Name, since it doesn't actually involve your voice at all.
        var wrappedMessage = Loc.GetString("chat-manager-entity-subtle-ooc-wrap-message",
            ("entityName", name),
            ("entity", ent),
            ("message", FormattedMessage.RemoveMarkup(action)));

        foreach (var (session, data) in GetRecipients(source, WhisperClearRange, ghostsCanHear: false))
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            if (MessageRangeCheck(session, data, range) == MessageRangeCheckResult.Disallowed)
                continue;

            _chatManager.ChatMessageToOne(ChatChannel.Emotes, action, wrappedMessage, source, false, session.Channel);
        }

        if (!hideLog)
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Subtle OOC from {ToPrettyString(source):user} as {name}: {action}");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Subtle OOC from {ToPrettyString(source):user}: {action}");
    }
    // Floof - end

    private void SendDeadChat(EntityUid source, ICommonSession player, string message, bool hideChat)
    {
        if (!_adminManager.IsAdmin(player) && !_deadChatEnabled)
            return;

        var clients = GetDeadChatClients();
        string wrappedMessage;
        if (_adminManager.IsAdmin(player))
        {
            var userName = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(player.Channel.UserName)}\" entity=\"{GetNetEntity(source)}\" color=\"{ChatChannel.Dead.TextColor().ToHex()}\"]" : FormattedMessage.EscapeText(player.Channel.UserName);
            wrappedMessage = Loc.GetString("chat-manager-send-admin-dead-chat-wrap-message",
                ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")),
                ("userName", (userName)),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Admin dead chat from {source}: {message}");
        }
        else
        {
            var playerName = ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(Name(source))}\" entity=\"{GetNetEntity(source)}\" color=\"{ChatChannel.Dead.TextColor().ToHex()}\"]" : FormattedMessage.EscapeText(Name(source));
            wrappedMessage = Loc.GetString("chat-manager-send-dead-chat-wrap-message",
                ("deadChannelName", Loc.GetString("chat-manager-dead-channel-name")),
                ("playerName", (playerName)),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Dead chat from {source}: {message}");
        }

        _chatManager.ChatMessageToMany(ChatChannel.Dead, message, wrappedMessage, source, hideChat, true, clients.ToList(), author: player.UserId);
    }
}
