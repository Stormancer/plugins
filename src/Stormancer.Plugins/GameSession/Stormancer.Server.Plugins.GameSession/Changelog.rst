=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

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
