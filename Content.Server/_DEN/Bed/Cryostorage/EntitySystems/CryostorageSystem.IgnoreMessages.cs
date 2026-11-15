using Content.Server._DEN.Bed.Cryostorage.Components;
using Content.Shared._DEN.Bed.Cryostorage;

namespace Content.Server.Bed.Cryostorage;

public sealed partial class CryostorageSystem
{
    private void InitializeIgnoreMessage()
    {
        SubscribeNetworkEvent<IgnoreCryoMessage>(OnIgnoreRequest);
    }

    private void OnIgnoreRequest(IgnoreCryoMessage msg, EntitySessionEventArgs args)
    {
        var senderSession = args.SenderSession;

        // Whoever sent this actually has a character.
        if (senderSession.AttachedEntity is null)
            return;

        var entity = senderSession.AttachedEntity.Value;

        if (msg.Ignore)
            AddComp<IgnoringCryoMessagesComponent>(entity);
        else
            RemComp<IgnoringCryoMessagesComponent>(entity);
    }
}