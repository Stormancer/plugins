Steam
-----

# Server Application

## Secrets

### Get base URI from script arguments

```ps
# Get BaseUri
if ($args.count -lt 1)
{
    echo "Missing Uri argument"
    return 1
}
$baseUri = $args[0]
```

### Steam Api Key

Format : 32 hexadecimal characters.

```ps
Invoke-RestMethod -Method Put -Uri "$baseUri/_secrets/[GameOrPublisherAccount]/secrets/steam_apiKey" -ContentType "text/plain" -InFile "$PSScriptRoot\Secrets\steam_apiKey.txt"
```

`[GameOrPublisherAccount]` should be replaced by the Game or Publisher Stormancer account.

### Steam Lobby metadata bearer token key

Use it when you want to enable joining Stormancer parties from Steam lobby invitations.

Format : 32 bytes (binary)

```ps
$steamBearerTokenPath = "$PSScriptRoot\Secrets\steam_lobbyMetadataBearerTokenKey"
if (-not(Test-path "$steamBearerTokenPath" -PathType leaf))
{
    dotnet tool run stormancer manage secrets generate --output "$steamBearerTokenPath" --size 32
}
Invoke-RestMethod -Method Put -Uri "$baseUri/_secrets/[GameOrPublisherAccount]/secrets/steam_lobbyMetadataBearerTokenKey" -ContentType "application/octet-stream" -InFile "$steamBearerTokenPath"
```

`[GameOrPublisherAccount]` should be replaced by the Game or Publisher Stormancer account.

<span style="color:red">**BE CAREFUL, The key will be created randomly and overwritten by the script if it doesn't exist, so don't forget to backup it on first setup!**</span>

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
