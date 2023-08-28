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
- Updated dependency to Users to support new abstraction lib.
- Change the format of custom profile part ids.

Added
*****
- Added 'userId' field to the user profile part.


4.0.0.3
-------
Changed
*****
- Update GetProfilesBySessionIds API to expect a sequence of SessionId objects instead of a sequence of string.

3.0.0.4
----------
Changed
*******
- Remove dependency to Stormancer.Abstraction.Server
- Changed Profile dto to include a field stating if the profile was found or not.
- Fix Profiles to Profile in many places to have a better coherence.

Fixed
*****
- Create empty entries in GetProfiles for missing profiles.

2.4.2.2
-------
Changed
*******
- Deterministic build

2.4.2.1
-------
Changed
*******
- Optimize PseudoProfilePart
Fixed
*****
- PseudoProfilePart: Handle not found users in db

2.4.1.2
-------
Added
*****
- Added field lastPlatform in user profile part
Changed
*******
- Improve user profile part

2.3.1
-----
Added
*****
- Added Profile.GetProfilesBySessionIds RPC to query profiles using a list of session ids.
- Added "platform" field in UserProfilePart

2.2.0.2
-------
Changed
*******
- Added support for cancellation
- Support latest Users API.
- Use Stormancer.Abstraction.Server 5.0.0

Added
*****
- Added ProfileProxy class to access profile features in S2S.

2.1.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.2.0.2-pre
2.1.0.6
-------
Added
*****
- Add ICustomProfilePart interface to easily provides update/delete API for profiles using the ProfileController Update/Delete APIs.
- Add CustomPartAttribute to declare class as profile parts.
- Automatically create the profile scene on startup and adds service locator configuration.

2.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

2.0.0.1
----------
Changed
*******
- Update to .NET5.0

1.1.6
-----
Added
*****

Changed
*******
- New versioning system

Removed
*******

