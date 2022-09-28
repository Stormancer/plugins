=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

0.2.0.1
----------
Changed
*******
- Update dependency to Stormancer.Abstraction.Server 9.0.0

0.1.8.1
-------
Changed
*******
- Deterministic build

0.1.8
-----
Changed
*******
- Use new S2S infrastructure to get limit status.
Fixed
*****
- Add a IServiceLocationProvider to always locate the limits S2S Api on the authenticator scene.

0.1.6
-----
Changed
*******
- Supports Abstractions.Server 5.0.0

0.1.5
-----
- Use Stormancer.Abstraction.Server 4.2.0
0.1.4
-----
- Use Stormancer.Abstraction.Server 4.1.0


0.1.3.1
-------
Changed
*******
- cache results of calls to GetUserLimitsStatus

0.1.3
-----
Changed
*******
- Fixed issue that could decrement user count even if user skipped queue.

0.1.2
-----
Changed
*******
- Properly report connection count even when limits are disabled.

0.1.1.1
-------
Added
*****
- Enforce concurrent sessions limit from the configuration.
- Queue users over the sessions limit.
- Report concurrent sessions limits status with ILimits and an admin web API, including queue length and average time between user entry.
- Added Licence expression to nuget package.

