=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

2.0.2
-----
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

