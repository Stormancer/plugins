Steam
-----

# Party

Setup the party on creation event :

```cs
public class PartyEventHandler : IPartyEventHandler
{
    public Task OnCreatingParty(PartyCreationContext ctx)
    {
        ctx.PartyRequest.ServerSettings
            .ShouldCreatePlatformLobby(true)
            .MaxMembers(2)
            .SteamLobbyType(LobbyType.FriendsOnly);

        return Task.CompletedTask;
    }
}
```
