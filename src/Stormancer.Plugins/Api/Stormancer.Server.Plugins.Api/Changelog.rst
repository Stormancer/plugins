=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

3.0.0.1
----------
Changed
*******
- Update dependency to Stormancer.Abstraction.Server 9.0.0

Added
*****
- Add support to IScenePeerClient as argument in controller actions.
Remove
******
- Remove Support for deprecated S2S API using RPCService

2.3.1.2
-------
Changed
*******
- Deterministic build

2.3.1.1
-------
Fixed
*****
- Fixed potential duplicate S2S action execution.

2.3.0
-----
Changed
*******
- Remove 3 allocations in action call expression generation.
Added
*****
- Support for CancellationToken as parameter for RPC controller actions.

Added
*****
- Action call generation now maps CancellationToken action parameters to IS2SRequestContext.CancellationToken.

2.1.0
-----
Added
*****
-  Add support for new S2S protocol.

2.0.2
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

2.0.1
----------
Changed
*******
- Throwing `OperationCanceledException` in an API resulted in an unknown error on the client as well as an error log on the server. However, throwing OperationCanceledException is the normal result of cancellation. The client now receives a `canceled` error and no log is written.

2.0.0.1
----------
- Update to .NET5.0

1.3.1
-----
Added
*****

Changed
*******
- Set Peer in ControllerBase when OnConnected or OnDisconnected are triggered.

Removed
*******

