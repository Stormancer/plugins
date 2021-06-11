=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

5.0.1.1
-------
Fixed
*****
- Fixed deadlock in UserSessions.SendRequest.

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

