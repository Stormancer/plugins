=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

5.0.2
-----
Changed
*******
- When querying a leaderboard by id, if the id is not found, return an empty result instead of throwing an exception.
- GetScores takes an IEnumerable<string> instead of a List<string> for its ids.

Fixed
*****
- Don't allow the score updater to change the current score object, but have it work on a clone.
- Uses correct ids in GetScores

4.0.2.4
-------
Changed
*******
- Deterministic build

4.0.2.3
-------
Changed
*******
- Remove unnecessary logs

4.0.2.2
-------
Changed
*******
- Remove debug logs

4.0.2.1
-------
Fixed
*****
- if score and timestamp are equal, compare guids.

4.0.1
-----
Added
*****
- CancellationToken added to query APIs
- Support for friendsOnly option to automatically filter a leaderbard by the friend list managed by Stormancer.Server.Plugins.Friends.
- Add mySelf in the friends only query.

Changed
*******
- Fix UserId in LeaderboardQuery.
- Fix AdjustQuery
- Fix Use FilteredUserIds instead of FriendIds in LeaderboardQuery

3.0.2.1
-------
Changed
*******
- Use Stormancer.Abstraction.Server 5.0.0

3.0.2
-----
- Use Stormancer.Abstraction.Server 4.2.0.2
3.0.1
-----
- Use Stormancer.Abstraction.Server 6.1.0

3.0.0.1
-------
Changed
*******
- Update to .NET5.0

2.1.3.4
-------
Added
*****

Changed
*******
- New versioning system

Removed
*******

