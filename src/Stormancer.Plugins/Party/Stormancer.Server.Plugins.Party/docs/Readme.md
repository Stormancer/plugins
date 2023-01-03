#Parties

This plugin adds player parties related features to a Stormancer application. It contains a Server application plugin (the [Nuget package](https://www.nuget.org/packages/Stormancer.Server.Plugins.Party)), and a C++ client plugin ([Party.hpp](https://raw.githubusercontent.com/Stormancer/plugins/develop/src/Stormancer.Plugins/Party/cpp/Party.hpp)).

A player party is a group any player can create, which can be joined by other players afterward. A gamefinder is set as part of the party creation parameters and can be changed afterwards. 
When all members of a party are in the ready state (set by calling `PartyApi::updatePlayerStatus`), the selected gamefinder is executed, with the current party data (including party settings, member list and member data) as argument.

The party:

- Maintains on all party members as well as on the server an updated list of the party members
- Maintain a party leader, with leadership automatically passed to another player in case of leader disconnection
- Synchronizes custom party settings (expressed as a string). Only the leader can update the party settings.
- Synchronizes custom data (as a string) for all members. Only the corresponding member can updates its data.
- Generates invitation codes to enable players to join in a crossplay game.
- Synchronizes an extensible state, for instance filled by the Gamesession system so that players joining a party already in a gamesession can detect that and join the gamesession.
- Integrates with the invitation systems of gaming platforms like Steam, PSN, XBoxLive, Nintendo Online and Epic Store to provide a seamless experience for players running the game on the same platform, while retaining crossplay capabilities in the same party.
- Provides search and filtering capabilities based on Lucene.

The party system uses Stormancer scenes as its core abstraction (a party is a component of a scene). This makes the party system highly scalable and extensible as developers can easily add additional behaviors to a party scene.

## Invitation codes

Customizing invitation codes:


	{
		"party":{
			"authorizedInvitationCodeCharacters":"01234567890" // defaults to "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"
			"invitationCodeLength" : 4 //defaults to 6
		}
	}


## Joining the gamesession your party is in

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


## Party Search

Once a party is created, the party leader can index it into an a distributed in memory database on the server using Lucene for search. To do that, call `UpdatePartySettings` and provide a valid json document in the `indexedDocument` field.

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

## Validating party settings and party member data

Party settings and party member data can be validated by creating a class implementing `IPartyEventHandler` and the methods `OnUpdatingSettings` and `OnUpdatingPartyMemberData`. Both APIs support denying the update and providing an error string sent back to 

## Customizing ready state changes

### Validating setting the player state as ready

The state of a player can be validated when they try setting themselves as ready by implementing `IPartyEventHandler.OnUpdatingPlayerReadyState`. This method can perform validation and set a custom error string to be sent back to the client in case of failure.

### Customizing ready reset.

By default, the ready state resets to `NotReady` if any of the following conditions are met:
- The list of players in the party changes.
- Party settings are updated.
- The custom data of any party members are updated.

This behavior can be customized through two mechanisms:
- Party configuration
- The Party `IPartyEventHandler` extensibility point.

Note that preventing reset of the ready state also disable automated matchmaking cancellation in all the situations that would have reset it, for instance if a player in the party disconnects while matchmaking is in progress. However as data about the parties are sent to the
matchmaker only when matchmaking starts, they may become out of date. Manually resetting the ready state still cancels matchmaking as only automated reset is disabled.

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

#### A IPartyEventHandler implementation.

Implement `IPartyEventHandler.OnPlayerReadyStateReset` in an event handler to control when the the ready state should be reset depending on custom logic.