=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.


Unreleased
----------
Changed
*******
- Updated dependency to Users to support new abstraction lib.
- Remove current gamesession in party when the gamesession scene shuts down.
- If party does not exist, CreateConnectionToken returns 'notFound'
- Removes an error if a user has no profile when entering the party
- Improved Error logs in case of gamefinder failures.

Fixed
*****
- Use PeriodicTimer in PartyAnalyticsWorker to fix an high usage CPU issue.
- Don't create an error log if the party of a player was already destroyed when a gamesession try to update its party state.
- Fixed issue in party size check that prevented the connecting player from being taken into account.
- Made the plugin compatible with Users 9.0.0.
- Don't create a new reservation cleanup loop each time a PartyService is created.

Added
*****
- Added Party Id to PartySettingsUpdateDto
- Added LocalPlayerCount to party user data to enable party members to be associated to several local players.
- Added default implementations to several methods of `IPartyEventHandler`
- Added TryGetGamesessionId extension method to party.
- Added extensibility point IPartyEventHandler.OnCreatingReservation 
- Added a section on common issues in the documentation.
- Added a reason argument to the method 'kick from party'.
- Check the crossplay options when joining the party.

5.0.1.7
-------
Fixed
*****
- Send back errors to  client when an operation fails.
- Fix typo that could prevent steam users from joining parties.
- Always remove party from index on shutdown.
- Also consider the players whose connection is currently being validated when checking for max member count.
- Fix to concurrency issues

5.0.0.2
-------
Changed
*******
- Send IndexedDocument as part of party updated events.
- Add configuration and event handler extensibility points to control when ready state is reset, validate ready state changes and user data updates.
- Parties are now destroyed immediatly after the last player left to prevent players from joining afterwards.
- UpdateSettings method now takes a PartyState object as lambda argument.
- PartyUpdateDto new takes a PartyState object as lambda argument.
- Automatically destroy scene when last player disconnects.

Added
*****
- Add arguments to set member data on party creation or on joining.


4.3.1.13
--------
Added
*****
- Added 'party.enableGameSessionPartyStatus' to app configuration to disable gamesession party status (it disables joining party gamesession too). Defaults to true (enabled) by default.
Changed
*******
- Created factory lambda for Autofac dependencies to improve resolution speed.

4.3.1.12
----------
Changed
*******
- Update dependency to Stormancer.Abstraction.Server 9.0.0
- Only generate numbers in invitation codes.
- Only leader can create and cancel invitation codes.
Added
*****
- Added JoinCurrentGameSession : This functionality enables connecting to the game session to which other players in the party are currently connected.
- Added Party status to PublicServerData to track if the party is currently in a gamesession.
- Added configuration system.
- Added Party search system and API.
- PartySearch now returns both SearchDocument and party custom data.

4.1.4.2
-------
Changed
*******
- Deterministic build

4.1.4.1
-------
Changed
*******
- Fix handlers (use a request scope in sequential operations)
- FindGame returns false and message error on failure
- Add reason on party error (format: party.joinDenied?reason=xxxxxxxx)

4.1.2.1
-------
Changed
*******
- PartyService use PartyController cancellation tokens

4.1.2
-----
Changed
*******
- Fix handlers in case scope destroyed
- PartyService use PartyController cancellation tokens

4.1.1.6
-------
Changed
*******
- Catch OperationCanceledException instead of TaskCanceledException to avoid useless server log spams when FindGame is canceled by clients.

4.1.1.5
-------
Changed
*******
- Add Reason in JoiningPartyContext

4.1.1.4
-------
Fixed
*****
- Let Gamefinder proxy locate the gamefinder scene from the service name instead of doing that in the party plugin.

Changed
*******
- Use latest Models library

4.1.0.5
-------
Added
*****
- Invite players in the group using an invitation code.
Changed
*******
- Party scene name starts with "party-"

4.0.4.2
-------
Changed
*******
- Use Users 5.x
- Use Stormancer.Abstraction.Server 5.0.0

4.0.3
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.2-pre.
4.0.2
-----
Changed
*******
- Trace Log added (server.PartyService.OnConnecting) when a player tries to connect to a non joinable party.

4.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.1.0

4.0.0.2
-------
Changed
*******
- Update GameFinder dependency.

4.0.0.1
----------
Changed
*******
- Update to .NET5.0

3.2.2.4
-------
Changed
*******
- Don't output an error log when a party member disconnects during matchmaking.
- Improved error message if gamefinder not set in party creation.
Added
*****
- Automatically create party management scene in application if party plugin is installed.
- Automatically register the party management scene in the scene locator.

