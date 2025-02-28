=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

2.0.1.5
----------
- Updated to latest SDK.

2.0.1.4
-------
Changed
*******
- Use latest version of memory cache.

2.0.1.3
----------
Changed
*******
- Remove dependency to Stormancer.Abstraction.Server
Fixed
*****
- Nonce wasn't properly generated when missing from the configuration.

2.0.0.2
-------
Changed
*******
- Deterministic build

2.0.0.1
-------
Fixed
*****
- Change separator to avoid base64 character

2.0.0
-----
Changed
*******
- Use the secrets store API to store the encryption key.
- By default, auto generate the key and store it in the configured secret store. Set 'createKeyIfNotExists' in the policy to false to prevent this behavior.

1.0.1.1
-------
Changed
*******
- Use Stormancer.Abstraction.Server 5.0.0

1.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 4.1.0

1.0.0.1
----------
- Update to .NET5.0

0.1.1.1
-------
Added
*****

Changed
*******
- New versioning system

Removed
*******

