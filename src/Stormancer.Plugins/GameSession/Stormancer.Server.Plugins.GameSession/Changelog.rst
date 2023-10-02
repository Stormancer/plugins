=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

Unreleased
----------
Added
*****
- Added support for crash reports in game server agent.
- Exclude agents for 30s when they failed to create a server
- Kill agents if they fail twice in succession, indicating they are faulty.
- Added `IGameSessionEventHandler.OnGameSessionShutdown` event fired during scene shutdown.
- Added `IGameSessionEventHandler.OnGameSessionReset` event fired when the gamesession is reset by the host.
- Added `IGameSessionEventHandler.ShouldCompleteGame` event fired to decide if the gamesession should evaluate the results posted with PostResult and complete the game.
- Added property `CreatedOn` on `IGamesessionService` to get the UTC date the gamesession was created.
- Added Session to context of `IGameSessionEventHandler.OnGameSessionCompleted`.
- Added support for regions.
Changed
*******
- Removed a retry when creating a game session.
- Declared game servers as "service" clients to disable client related features, for instance version checking.

Fixed
*****
- Fixed infinite loop in the analytics worker loop.
- Fixed an wrong null check on closing servers that could lead to a NullRef exception when updating the game server auditing record.
- When trying to start a game server, timeout if it takes more than 5s to do a docker start on an agent.
- Fixed false positives error logs.
- Resetting a game session force the gamesession to be completed if it wasn't already, exactly as if all players add left it. This guarantees that if only the host sent results, they would be processed on reset, triggering (for instance) a PSN Activity completion.
- Made the plugin compatible with Users 9.0.0

6.1.1.15
----------
Changed
*******
- Add MaxPlayersInGameSession analytics
- Notify gameserver hosting agents when the app is not an active deployment.
- Make sure that calling IsHost never throws, but instead returns false.
- Don't send Teams infos to analytics
- Add gamefinder and Parameters to analytics
- Added the affected Players to the ReservationCancelledContext instead of only their user id.
- P2P is now the default mode if a gameserver couldn't be located.

Fixed
*****
- Don't wait 5s before returning PostResults response for the last player in the session.


6.1.0.17
--------
Changed
********
- Updated dependency to Stormancer.Abstractions.Server to 9.2
- Docker: Support pulling from dockerHub
- Docker: Use host network mode on Linux

Added
*****
- Added IsPersistentServer configuration to set wether the server of a gamesession should be stopped and the scene destroyed when the last player disconnects.
- Added More analytics
- Added Agent based game server hosting for better robustness and flexibility.

6.0.2
-----
Added
*****
- Added a new Analytics event (type 'gamessesion', category 'started') to gamesessions.

6.0.1.1
-------
Added
*****
- Add CreateP2PToken API to get a P2P token for any player in the gamesession.
Fixed
*****
- Fixed concurrency issue in OnConnected

6.0.0.2
----------
Added
*****
- GameFinder name has been added to gamesession's initialization configuration.
- Rework of server pools
- Add back game server support in gamesession.
- Add support for game servers running on local docker.
- Add EnableDirectConnection configuration value to disable P2P connection.
- Add new ServerReady event handler triggered when using a gamesession with a server.


Changed
*******
- Fix GameSessionService.PeerConnectionRejected
- Use GameSessionController to register to OnConnecting/ed disconnecting events.
- Call OnClientLeaving when peer disconnecting
- If DirectConnectionEnabled is selected, we don't wait for the host to connect, and we don't send any p2p token
- Update dependency to Stormancer.Abstraction.Server 9.0.0

Fixed
*****
- Don't poll docker status until a docker server gets started.
- Send "ready" server status update when a player connects if the game has alreay started

5.3.0
-----
Added
*****
- Added IGamesessions.CreateReservation and IGameSessions.CancelReservation methods to create and cancel reservations to open gamesessions.

5.2.0
-----
Added
*****
- added 'GameSession.GetTeams' public RPC route to get the list of players registered in the gamesession.

5.1.0.1
-------
Added
*****
- Added player reservation API.

5.0.1.2
-------
Changed
*******
- Make plugin compatible with Users 5.x
- Use Stormancer.Abstraction.Server 5.0.0

5.0.0
-----
Removed
*******
- Removed deprecated OpenToGameFinder API. Use GameFinderProxy.OpenGameSession instead.

4.0.7
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.
4.0.6
-----
Changed
*******
- Fixed timeout issue with gamesession registrations to gamefinder when the gamefinder sent team updates.

4.0.4.1
-------
Changed
*******
- update dependencies.

4.0.4
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

4.0.3.2
-------
Changed
*******
- Declare GameSessionService at scene level.
- Don't read size from postResult input because it's not supported anymore. TODO: We should check the size of the input.
- Add Licence expression to nuget package.

4.0.2
-----
Changed
*******
- Fix To allow postResults to be processed after a reset
- Changed Behavior: do not disconnect users from the game session to allow them to restart a game.
- Update to .NET5.0

3.3.2.2
--------
Changed
*******
- Register dependencies in host (instead of scene) to prevent factory code generation each time a scene is created.

Added
*****
- ``GameSessionConfigurationDto`` now has an additional ``HostUserId`` member. This member will be set in the object returned by ``GameSessionService.GetGameSessionConfig()`` if the game session has P2P enabled.
- ``IGameSessionEventHandler``: new ``OnClientConnected()`` method.
- New ``IGameSessionService.OpenToGameFinder()`` method. Allows adding new players to the session after it has started.
