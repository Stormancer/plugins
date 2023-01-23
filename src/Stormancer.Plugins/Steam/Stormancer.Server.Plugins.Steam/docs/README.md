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

## Party configuration

Setup the party using a party creation event handler :

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

# C++ client

## Plugin configuration

By default steam.hpp includes `steam_api.h`. This behavior can be disabled by setting `STORM_NOINCLUDE_STEAM`.

Configuration keys:

```c++
	/// <summary>
    /// Keys to use in Configuration::additionalParameters map to customize the Steam plugin behavior.
    /// </summary>
    namespace ConfigurationKeys
    {
        /// <summary>
        /// Enable Steam authentication.
        /// If disabled, the Steam plugin will not be considered for authentication.
        /// Default is "true".
        /// Use "false" to disable.
        /// </summary>
        constexpr const char* AuthenticationEnabled = "steam.authentication.enabled";

        /// <summary>
        /// The lobbyID the client should connect on authentication. 
        /// Automatic connection to a Steam lobby on successful authentication should occur when the game has been launched by a lobby invitation.
        /// You can get the LobbyID by searching the "+connect_lobby" parameter in the command line arguments (argv).
        /// </summary>
        constexpr const char* ConnectLobby = "steam.connectLobby";

        /// <summary>
        /// Should Stormancer initialize the Steam API library.
        /// Default is "true".
        /// Use "false" to disable.
        /// </summary>
        constexpr const char* SteamApiInitialize = "steam.steamApi.initialize";

        /// <summary>
        /// Should Stormancer run Steam Api callbacks.
        /// Default is "true".
        /// Use "false" to disable.
        /// </summary>
        constexpr const char* SteamApiRunCallbacks = "steam.steamApi.runCallbacks";
    }
```

## Enable steam invitations on game launch

```c++
int main(int argc, char* argv[])
{
	auto config = Stormancer::Configuration::create(STORM_ENDPOINT, STORM_ACCOUNT, STORM_APPLICATION);
	for (int argi = 0; argi < argc; argi++)
	{
		config->processLaunchArguments.push_back(argv[argi]);
	}
}
```
