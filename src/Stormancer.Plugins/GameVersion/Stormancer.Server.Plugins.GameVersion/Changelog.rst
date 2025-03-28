=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.


2.3.0
----------
Changed
*******
- Updated dependency to Users to support new abstraction lib.

Added
*****
- Added support for multiple supported version strings, through the `clientVersion.authorizedVersions` config value.
- Added a configuration section class that inherits from IConfigurationSection<T>

2.2.0.1
-------
Changed
*******
- Do not control the version on services clients.

2.1.0.1
----------
Changed
*******
- Remove dependency to Stormancer.Abstraction.Server
- Support '*' in required server string for to implment prefix matching. 1.0.* matches 1.0.1, 1.0.2, etc...

2.0.3.3
-------
Changed
*******
- Deterministic build

2.0.3.2
-------
Changed
*******
- Uses Users 5.x API
- Use Stormancer.Abstraction.Server 5.0.0

2.0.2
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.2-pre.
- Add a log when a user tries to connect with a bad game version

2.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.1.0.

2.0.0.1
-------
Changed
*******
- Update to .NET5.0

1.1.2
-----
Added
*****

Changed
*******
- New versioning system

Removed
*******

