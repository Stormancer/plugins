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
- Update dependencies to release version

0.3.0.2
----------
Added
*****
- Added support for P2P.
0.1.4.5
----------
- Remove dependency to Stormancer.Abstraction.Server

0.1.4.2
-------
Changed
*******
- Deterministic build

0.1.4.1
-------
Changed
*******
- Broadcast message action sends the SessionId as a SessionId object instead of a string.
Fixed
*****
- Fixed issue where entity broadcasts where not properly routed.

0.1.3.1
-------
Changed
*******
- Use Stormancer.Abstraction.Server 5.0.0

0.1.3
-----
Changed
*******
- Use Stormancer.Abstractions.Server 4.2.0.1-pre
0.1.2
------
Added
*****
- Initial plugin implementation: All peers are authorities and have views on the whole replication index.
- Added entrypoint
- Use Stormancer.Abstraction.Server 6.1.0
