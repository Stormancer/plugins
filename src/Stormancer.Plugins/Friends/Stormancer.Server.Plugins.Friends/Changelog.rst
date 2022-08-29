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
- Remove dependency to Stormancer.Abstraction.Server

4.0.0.1
-------
Changed
*******
- Deterministic build

4.0.0
-----
Changed
*******
- Manual subscribe to friends list

3.1.1
-----
- Add Unknow value to FriendStatus, FriendListStatusConfig and FriendRecordStatus enums.

3.1.0.1
-------
Added
*****
- Added GetFriends API for the server to get the friends of an user.
Fixed
*****
- Fix Friends notification api

3.0.0.2
-------
Changed
*******
- Updated Users dependency to 5.x
- Added a CancellationToken parameter to most async APIs.
- Use new S2S protocols internally and use code generator to create S2S proxy.
- Use Stormancer.Abstraction.Server 5.0.0

2.0.2
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.

2.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

2.0.0.2
-------
- Update to .NET5.0

1.0.9
-----
Added
*****

Changed
*******
- New versioning system

Removed
*******

