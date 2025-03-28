=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

2.3.1.3
----------
Added
*****
- Added DisableDirectStreaming connection configuration value to improve the debugging experience.

2.3.0
-----
Added
*****
- Added An exception handler to RPCs to log more detailed informations about Elasticsearch exceptions.

2.2.1.1
----------
- Remove dependency to Stormancer.Abstraction.Server
- updated NEST

Fixed
*****
-NullreferenceException in intitialization

2.1.0.3
-------
Changed
*******
- ES password can now be retrieved from the secrets store.

2.1.0.1
-----
Added
*****
- New GetAllLocal function in memory index to return all documents stored locally.

2.0.1.1
-------
Changed
*******
- Use Stormancer.Abstraction.Server 5.0.0

2.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.1.0

2.0.0.1
----------
- Update to .NET5.0

1.0.3.4
-------
Added
*****

Changed
*******
- New versioning system

Removed
*******

