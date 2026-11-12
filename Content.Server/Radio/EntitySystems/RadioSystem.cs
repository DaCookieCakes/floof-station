using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
// Starlight - Start
using Content.Shared._Starlight.Language;
using Content.Server._Starlight.Language;
// Starlight - End

namespace Content.Server.Radio.EntitySystems;

/// <inheritdoc/>
public sealed partial class RadioSystem : SharedRadioSystem
{
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private IReplayRecordingManager _replay = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityQuery<TelecomExemptComponent> _exemptQuery = default!;
    [Dependency] private LanguageSystem _language = default!; // Starlight
    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && component.Channels.Contains(args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid, args.Language); // Starlight
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (TryComp(uid, out ActorComponent? actor))
        {
            // Starlight - Start
            var listener = component.Owner;
            var chatMsg = args.OriginalChatMsg;

            if (!_language.CanUnderstand(listener, args.Language.ID))
                chatMsg = args.LanguageObfuscatedChatMsg;

            _netMan.ServerSendMessage(new MsgChatMessage { Message = chatMsg }, actor.PlayerSession.Channel);
            // Starlight - End
        }
    }

    /// <inheritdoc/>
    public override void SendRadioMessage(EntityUid messageSource,
        string message,
        RadioChannelPrototype channel,
        EntityUid radioSource,
        LanguagePrototype? language = null, // Starlight
        bool escapeMarkup = true)
    {
        // Starlight - start
        if (language == null)
            language = _language.GetLanguage(messageSource);

        if (!language.SpeechOverride.AllowRadio)
            return;
        // Starlight - End

        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return;

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        var name = evt.VoiceName;
        name = _chat.ChatNameLinks ? $"[textlink=\"{FormattedMessage.EscapeStringParameter(name)}\" entity=\"{GetNetEntity(messageSource)}\" color=\"{channel.Color.ToHex()}\"]" : FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && ProtoMan.Resolve(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        var wrappedMessage = WrapRadioMessage(messageSource, channel, name, content, language, false); // Starlight

        // most radios are relayed to chat, so lets parse the chat message beforehand
        var msg = new ChatMessage(ChatChannel.Radio, content, wrappedMessage, NetEntity.Invalid, null); // Starlight

        var obfuscated = _language.ObfuscateSpeech(content, language);
        var obfuscatedWrapped = WrapRadioMessage(messageSource, channel, name, obfuscated, language, true);
        var notUdsMsg = new ChatMessage(ChatChannel.Radio, obfuscated, obfuscatedWrapped, NetEntity.Invalid, null);
        var ev = new RadioReceiveEvent(messageSource, channel, msg, notUdsMsg, language, radioSource, []);
        // Starlight - End
        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceMapId = Transform(radioSource).MapID;
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
        }

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName}: {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName}: {message}");

        _replay.RecordServerMessage(msg); // Starlight-edit: Languages
        _messages.Remove(message);
    }

    // Starlight - Start
    private string WrapRadioMessage(
        EntityUid source,
        RadioChannelPrototype channel,
        string name,
        string message,
        LanguagePrototype language,
        bool obfuscated
        )
    {
        // TODO: code duplication with ChatSystem.WrapMessage
        var speech = _chat.GetSpeechVerb(source, message);
        var languageColor = channel.Color;

        if (language.SpeechOverride.Color is { } colorOverride)
            languageColor = Color.InterpolateBetween(Color.White, colorOverride, colorOverride.A); // Changed first param to Color.White so it shows color correctly.

        var namestring = $"{name}";
        if (_language.GetLanguageIcon(language, obfuscated))
            namestring = $"[icon src=\"{language.Icon}\" tooltip=\"{language.Name}\"] {name}";

        var fonttype = language.SpeechOverride.FontId ?? speech.FontId;
        if ((language.SpeechOverride.ObfuscationFont ?? false) && !obfuscated)
            fonttype = speech.FontId;

        return Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
                ("color", channel.Color),
                ("languageColor", languageColor),
                ("fontType", fonttype),
                ("fontSize", language.SpeechOverride.FontSize ?? speech.FontSize),
                ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
                ("channel", $"\\[{channel.LocalizedName}\\]"),
                ("name", namestring),
                ("message", message));
    }
    // Starlight - End

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }
}
