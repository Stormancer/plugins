=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.


3.1.1.3
----------
Changed
*******
- Updated dependency to Users to support new abstraction lib.
- Made the plugin compatible with Users 9.0.0.

Removed
*******
- Removed acknolewdge feature temporarely to fix depencency scope issue.

Fixed
*****
- Notification provider is new InstancePerRequest to fix a dependency loading issue with `IUserSession` which is itself InstancePerRequest.


3.1.0.10
----------
Changed
*******
- Remove dependency to Stormancer.Abstraction.Server


3.1.0.5
-------
Changed
*******
- Deterministic build

3.1.0.4
------
Added
*****
- Added admin web API.

3.0.0.4
-------
Changed
*******
- Add CancellationToken to INotificationProvider.Send() 
- Support Users 5.x API.
- Use Stormancer.Abstraction.Server 5.0.0

2.0.2
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.1-pre

2.0.1
-----
- Use Stormancer.Abstraction.Server 6.1.0

2.0.0.2
----------
Changed
*******
- Update to .NET5.0

1.0.2
-----
Added
*****

Changed
*******
- New versioning system

Removed
*******

