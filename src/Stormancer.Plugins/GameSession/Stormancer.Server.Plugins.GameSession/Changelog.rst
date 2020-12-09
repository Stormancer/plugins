=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

3.3.4
-----
Changed
*******
- Fix To allow postResults to be processed after a reset
- Changed Behavior: do not disconnect users from the game session to allow them to restart a game.
- Register dependencies in host (instead of scene) to prevent factory code generation each time a scene is created.

Added
*****
- ``GameSessionConfigurationDto`` now has an additional ``HostUserId`` member. This member will be set in the object returned by ``GameSessionService.GetGameSessionConfig()`` if the game session has P2P enabled.
- ``IGameSessionEventHandler``: new ``OnClientConnected()`` method.
- New ``IGameSessionService.OpenToGameFinder()`` method. Allows adding new players to the session after it has started.
