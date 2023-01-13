# Overview
This module adds player parties related features to a Stormancer application. It contains a Server application plugin (the [Nuget package](https://www.nuget.org/packages/Stormancer.Server.Plugins.Party)), and a C++ client plugin ([Party.hpp](https://raw.githubusercontent.com/Stormancer/plugins/develop/src/Stormancer.Plugins/Party/cpp/Party.hpp)).

A player party is a group any player can create, which can be joined by other players afterward. A gamefinder is set as part of the party creation settings and can be changed afterwards. 
When all members of a party are in the ready state (set by calling `PartyApi::updatePlayerStatus`), the selected gamefinder is executed, with the current party data (including party settings, member list and member data) as argument.

Main features:
- Maintains on all party members as well as on the server an updated list of the party members
- Maintains a party leader, with leadership automatically passed to another player in case of leader disconnection
- Synchronizes custom party settings (expressed as a string). Only the leader can update the party settings.
- Synchronizes custom data (as a string) for all members. Each party member can only update its own data.
- Generates invitation codes to enable players to join in a crossplay game.
- Synchronizes an extensible state, for instance filled by the Gamesession system so that players joining a party already in a gamesession can detect that and join the gamesession.
- Integrates with the invitation systems of gaming platforms like Steam, PSN, XBoxLive, Nintendo Online and Epic Store to provide a seamless experience for players running the game on the same platform, while retaining crossplay capabilities in the same party.
- Provides search and filtering capabilities based on Lucene.

The party system uses Stormancer scenes as its core abstraction (a party is a component of a scene). This makes the party system highly scalable and extensible as developers can easily add additional behaviors to a party scene.


# Creating a party

    client->DependencyResolver.resolve<Stormancer::PartyApi>()->createIfNotJoined().then([]()
	{
		...
	});

# Joining a party
## Invitation codes

Parties can be joined in a cross platform way by using invitation codes
Invitation codes can be customized in the server configuration, or code:


	{
		"party":{
			"authorizedInvitationCodeCharacters":"01234567890" // defaults to "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"
			"invitationCodeLength" : 4 //defaults to 6
		}
	}

To generate an invitation code, the party leader calls `pplx::task<std::string> PartyApi::createInvitationCode(pplx::cancellation_token ct)`. Calling the method again invalidates the previous code. 
The current invitation code can be disabled without generating a new one by calling `pplx::task<void> PartyApi::cancelInvitationCode(pplx::cancellation_token ct = pplx::cancellation_token::none())`

To join a party using a code, users call `pplx::task<void> joinPartyByInvitationCode(const std::string& invitationCode)` and can optionally provide custom member data.

## Platform integration

The party integrates with the platform plugins (steam.hpp, epic.hpp, etc...) to process player invitations. Each platform requires specific setup described in the relevant documentation. 
Additionnally, the game must register a callback to the `OnInvitationReceived` event by calling `Subscription PartyApi::subscribeOnInvitationReceived(std::function<void(PartyInvitation)> callback)` and storing the `Subscription` object returned by the method
for as long as the game should listen to the event (Most of the time as long as the game is running).

When an invitation is received, the game should either cancel it, or process it by calling `PartyInvitation::acceptAndJoinParty()` optionally providing custom memberData or `PartyInvitation::decline()`

Furthermore, a list or currently pending invitations can be retrieved by calling `PartyApi::getPendingInvitations()`

# Gamefinder integration

Gamefinders take player parties as arguments for matchmaking. A gamefinder id is associated with the party on party creation by setting `PartyCreationOptions::gameFinder` and is stored in the party settings. 
After party creation the party leader can change it by updating the party settings.

Once all player have set their state as ready by calling  `updatePlayerStatus(PartyUserStatus playerStatus)` with playerStatus set as `PartyUserStatus::Ready`, the party automatically invokes the configured gamefinder. 

## Customizing ready state changes
The party system supports various ways in which gamefinder integration can be customized.

### Validating party state before setting the player state as ready

The state of a player can be validated when they try setting themselves as ready by implementing `IPartyEventHandler.OnUpdatingPlayerReadyState`. This method can perform validation and set a custom error string to be sent back to the client in case of failure.

### Customizing ready reset behavior

By default, the ready state resets to `NotReady` if any of the following conditions are met:
- The list of players in the party changes.
- Party settings are updated.
- The custom data of any party members is updated.
- Matchmaking is in progress

This behavior can be customized through two mechanisms:
- Party configuration
- The Party `IPartyEventHandler` extensibility point.

Note that preventing resetting the ready state also disables automated matchmaking cancellation in all the situations that would have reset it, for instance if a player in the party disconnects while matchmaking is in progress. Be carefult, as data about the parties are sent to the
matchmaker only when matchmaking starts, they may become out of synch in this case. Manually resetting the ready state still cancels matchmaking as only automated reset is disabled. You may use a custom validator to reject data modification while in matchmaking.

#### Party Configuration

The list of events the ready state should reset in can be set in the party configuration. `ResetPlayerReadyStateMode.None` and `ResetPlayerReadyStateMode.All` are convenient shortcuts. By default, the option is set as `ResetPlayerReadyStateMode.All

	public class TestPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += (IHost host) =>
            {
                host.ConfigurePlayerParty(p => p.ResetPlayerReadyStateOn(ResetPlayerReadyStateMode.PartySettingsUpdated | ResetPlayerReadyStateMode.PartyMemberDataUpdated | ResetPlayerReadyStateMode.PartyMembersListUpdated));
			}
		}
	}


It's also possible to provide a custom lambda to drive the reset logic. For instance :

	public class TestPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += (IHost host) =>
            {
                var value = ResetPlayerReadyStateMode.All; //Always reset
                host.ConfigurePlayerParty(p => p.ResetPlayerReadyStateOn(ctx =>
                {
                    switch (ctx.EventType)
                    {
                        case PartyMemberReadyStateResetEventType.PartySettingsUpdated:
                            if ((value & ResetPlayerReadyStateMode.PartySettingsUpdated) != 0)
                            {
                                ctx.ShouldReset = true;
                            }
                            break;
                        case PartyMemberReadyStateResetEventType.PartyMemberDataUpdated:
                            if ((value & ResetPlayerReadyStateMode.PartyMemberDataUpdated) != 0)
                            {
                                ctx.ShouldReset = true;
                            }
                            break;
                        case PartyMemberReadyStateResetEventType.PartyMembersListUpdated:
                            if ((value & ResetPlayerReadyStateMode.PartyMembersListUpdated) != 0)
                            {
                                ctx.ShouldReset = true;
                            }
                            break;
                    }
                }));
			}
		}
	}

#### IPartyEventHandler implementation.

Implement `IPartyEventHandler.OnPlayerReadyStateReset` in an event handler to control when the the ready state should be reset depending on custom logic.


# Joining the current gamesession

When joining a party, it's possible to know if it is currently in a gamession, and join this gamesession. 
If not used by the application, this behavior can be disabled by setting to application configuration value `party.enableGameSessionPartyStatus` to false.

The following methods of `PartyApi` enables clients to determine if their party is in a gamesession, and if it's the case, to get a connection token to it that can be consumed by the GameSession client API.

	/// <summary>
	/// Gets a boolean indicating if the party is currently in a gamesession.
	/// </summary>
	/// <returns></returns>
	virtual bool isInGameSession() = 0;

	/// <summary>
	/// If the party is in a gamesession, gets a token to connect to it.
	/// </summary>
	/// <param name="ct"></param>
	/// <returns></returns>
	virtual pplx::task<std::string> getCurrentGameSessionConnectionToken(pplx::cancellation_token ct = pplx::cancellation_token::none()) = 0;


# Party Search

Once a party is created, the party leader can index it into a scalable Lucene server-side index for search. To do that, call `UpdatePartySettings` and provide a valid json document in the `indexedDocument` field.

	struct PartySettings
	{
		std::string gameFinderName;
		std::string customData;
		bool onlyLeaderCanInvite = true;
		bool isJoinable = true;
		std::unordered_map<std::string, std::string> publicServerData; // Not in MSGPACK_DEFINE because cannot be set by the client

		/// <summary>
		/// Json document used to search the party.
		/// </summary>
		/// <remarks>
		/// Must be a valid json object.
		/// The party is not searchable if set to empty or an invalid json object.
		/// The content of the document are indexed using the field paths as keys, with '.' as separator.
		/// 
		/// For example, the following document:
		/// {
		///    "maxPlayers":3,
		///    "gamemode":{
		///      "map":"level3-a",
		///      "extraFooEnabled":true
		///    }
		/// }
		/// 
		/// will be indexed with the following keys:
		/// - "numplayers": 3 (numeric)
		/// - "gamemode.map":"level3-a" (string)
		/// - "gamemode.extraFooEnabled":true (bool)
		/// 
		/// To enable search without filtering, set indexedDocument to an empty json object '{}'.
		/// </remarks>
		std::string indexedDocument;

		MSGPACK_DEFINE(gameFinderName, customData, onlyLeaderCanInvite, isJoinable, indexedDocument);
	};

Parties can be searched by calling `PartyApi::Search()` with a json Stormancer filter as the argument. Only clauses compatible with the Lucene query parser are supported (see Stormancer.Server.Plugins.Queries).

	{
		"bool":{
			"must":[
				{
					"term":"numplayers",
					"value":2
				},
				{
					"term":"gamemode.map",
					"value":"level3-a"
				}]
		}

# Validating party settings and party member data

Party settings and party member data can be validated by creating a class implementing `IPartyEventHandler` and the methods `OnUpdatingSettings` and `OnUpdatingPartyMemberData`. Both APIs support denying the update and providing an error string sent back to the caller.

