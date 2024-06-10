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
- By default, data are not stored in any database anymore
- Use Stormancer.Server.Plugins.Users.EntityFramework to use entity framework to store data.
- Analytics now store realtime session metrics grouped per dimensions vector.
- Removed reference to Elasticsearch library.

Added
*****
- Dimensions are saved in the analytics system on login
- Added GetSelectedPlatformForPseudo to get the platform the profile system should use when computing the pseudonym.
Fixed
*****
- GetUsers doesn't crash when receiving invalid user ids.

Added
*****
- IAuthenticationEventHandler.OnLoggedIn now provides the peer.

8.0.0.2
----------
Changed
*******
- Update SendRequest to properly handle client exceptions

7.1.0
-----
Changed
*******
- Use latest version of MemoryCache, improve performance by using SessionId instead of string as cache key.

7.0.1.2
-------
Changed
*******
- Don't retry AddAuthentication. It caused very long failure time on authentication if the DB was not running.

7.0.0.9
----------
Fixed
*****
- Calling ConfigureUsers several times doesn't reset the config between two calls.
- Removed IUSerService.CreateUser() overload with only 2 arguments: Providers should always specify the current platform when creating users.
- Fixed possible deadlock when using SendUserRequest.
- Fixes an exception on credentials renewal
Added
*****
- AuthenticationProvider can add metadata to login results.
Changed
*******
- Target dotnet 6 and Stormancer.Abstractions.Server 8.0.0

5.2.3.12
--------
Changed
*******
- Deterministic build

5.2.3.11
--------
Changed
*******
- Add more precise debug logs when session not found

5.2.3.3
-------
Fixed
*****
- Remove log when session not found in release.
5.2.3.2
-------
Fixed
*****
- Fixed missing analytics events for login/logout.

5.2.3.1
-------
Changed
*******
- Add warn log when user session not found

5.2.3
-----
Changed
*******
- Fix renewCredentials stream uses

5.2.2.5
-------
Changed
*******
- Add property User.LastPlatform
- Fix GetUsersByClaim bad index configuration
- Add debug logs on renewCredentials failure

5.1.1.3
-------
Fixed
*****
- When kicking players, we need to use DisconnectFromServer (which as expected disconnects from the server ) and not Disconnect, which just disconnects from scene. (Should probably be renamed DisconnectFromScene...)
- GetSessions returns an empty entry for session ids not associated with a profile.
Added
*****
- Kick now supports the filter */authenticated which enables kicking only authenticated users. (queued users are not kicked) 
- Kick now supports the filter */!authenticated which enables kicking only non authenticated users. (queued users are kicked but not authenticated users for instance.) 

5.0.1.5
-------
Fixed
*****
- Fixed deadlocks in UserSessions.SendRequest.
- Fix public GetAuthenticatedUsersCount

5.0.0.13
--------
Changed
*******
- Use the new S2S API system and proxy generator.
- Use Stormancer.Abstraction.Server 5.0.0
Removed
*******
- UserSessionCache was removed because the new S2S system makes it unnecessary.
Added
*****
- Service locator fallbacks to querying ServiceLocatorHostDatabase if it wasn't able to find a scene for a service query through IServiceLocatorProvider .

4.7.0
-------
Changed
*******
- Support new S2S protocol
- Service locator queries all nodes to find scenes when no handler could find them.

4.6.0
------
Changed
*******
- Use Stormancer.Abstractions 4.2
Added
*****
- Automatically register scenes with controllers with [Service] attribute in the service locator.
Removed
*******
- Removed deprecated, unused UserManagementConfig class.

4.5.0.3
-------
Changed
*******
- Fixes to interfaces
- Updating user handles returns the new user handle.
- Add documentation for deviceIdentifier provider to tell which key to use in authParameters
- Authorize - . and _ in user handles.
- Return current pseudo by default on login.

4.4.3.1
----------
Added
*****
- Add configuration builder and config extension methods for ephemeral auth.
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

4.4.1.2
----------
Changed
*******
- Don't include authResult in login.success log.
- Add Licence expression to nuget package.

4.2.0
-----
Added
*****
- Key used to encode and decode bearer token is fetched from SA configuration.
4.1.0.1
-------
Added
*****
- New extensibility point: `IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationResult ctx)`. It is called after authentication and before session creation and provides a way for plugin to override authentication results.
Changed
*******
- IAuthenticationEventHandler now provides default no-op implementations so that implementers are able to only provide the methods they need.

4.0.0.3
----------
Changed
*******
- Update to .NET5.0

3.6.0
-----
Changed
*******

Added
*****
- Client Api in UserSessionController to create and validate a Bearer token containing the client User id.
Removed
*******

