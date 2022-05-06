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

# Enable steam invitations on game launch

```c++
int main(int argc, char* argv[])
{
    if (argc > 2)
    {
        if (strcmp(argv[1], "+connect_lobby") == 0 && strlen(argv[2]) > 0)
        {
            const char* steamIDLobby = argv[2];
            config->additionalParameters[Steam::ConfigurationKeys::ConnectLobby] = steamIDLobby;
        }
    }
}
```
