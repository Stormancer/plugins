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
Added
*****
- Added support for the new Steam Authentication tokens introduced with the Steamworks SDK 1.57. Supports both the new and old authentication protocol. Switch to the new protocol happens by adding a `version` field to the auth parameters with `v1` as the content.
- Added support for multiple appIds, the client advertises its appId when sending the authentication request. If no appId is set, it defaults to the appId provided in the server configuration (field `steam.appId`). Additional authorized appId are configured by setting the `steam.appIds` server configuration to a list of steam app ids.
- Added `defaultAuthProtocol` field to the steam auth config section. This enables developer to chose the default Steam auth protocol to use if the client doesn't provide it. Defaults to v0001, the oldest protocol. This can be useful if you use a modified steam.hpp in the game client that doesn't advertise its protocol version and doesn't use v0001.
- Don't call Steam leave lobby web API on player leave lobby because it doesn't work with webAPI tokens.

Changed
*******
- Do not emit an error log when the steam client miss some features.

4.1.4
----------
Fixed
*****
- Destroy Steam lobby when last Steam player leave the party.
Changed
*******
- Improve logging in case of lobby creation failure.
- Compatibility with Users version 8.0.0

4.1.3
-----
Changed
*******
- Add compatibility to Stormancer.Server.Plugins.Party 5.0.0

4.1.2.10
----------
Changed
*******
- Remove dependency to Stormancer.Abstraction.Server

4.1.1.2
-------
Changed
*******
- Deterministic build
Fixed
*****
- SteamConfigBuilder must inherits from  AuthProviderConfigurationBuilderBase<SteamAuthConfigurationBuilder>

4.1.1.1
-------
Fixed
*****
- SteamProfilePart: Handle not found users in db

4.1.1
-----
- Add cancellation token used by party

4.1.0.17
--------
Changed
*******
- Remove debug logs

4.1.0.16
--------
Changed
*******
- Lobby metadata token key stored in secrets store

4.1.0.13
--------
Added
*****
- Add optional maxFriendsCount parameter to GetFriendList functions
- Steam profile part now writes the platform in platforms field array in user profile part
Changed
*******
- Profile part returns a SteamId as a string (instead of a ulong)
- Fix User.LastPlatform in Auth provider when User already present in DB

4.1.0.6
-------
- Fix queryUserIds (no error if a steamId is not found in the system)
- Improve steam friends
- Add SteamFriendsEventHandler to IOC
- Fix SteamFriend dto (add missing MessagePackMember)
- Fix OnGetFriends event handler
- Added ConfigureSteam extension method
- Clean SteamPartyEventHandler
- Add "platform" field in userData on authentication

4.0.4
-----
Changed
*******
- Update to Users 5.x
- Use Stormancer.Abstraction.Server 5.0.0

4.0.3
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.2.
4.0.2
-----
Changed
*******
- Improve auth update user data only if necessary (check multiple fields)
- Improve steam player profiles
- Profile part fixes
- Use Stormancer.Abstraction.Server 6.1.0

4.0.0.1
-------
Changed
*******
- Update to .NET5.0

3.0.5.4
-------
Added
*****

Changed
*******
- New versioning system
- Remove error logs when client provides invalid token.

Removed
*******
