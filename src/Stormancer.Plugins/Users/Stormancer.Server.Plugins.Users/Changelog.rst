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
- Add configuration builder and config extension methods for ephemeral auth.

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

