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

