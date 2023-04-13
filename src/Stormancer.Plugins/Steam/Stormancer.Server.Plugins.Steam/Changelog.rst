=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

Unreleased
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
