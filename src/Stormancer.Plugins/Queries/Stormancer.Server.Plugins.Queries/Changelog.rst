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

0.5.0.5
----------
- Updated to latest SDK.

0.5.0.2
----------
Changed
*******
- Update dependency to Stormancer.Abstraction.Server 9.0.0
- Allow Document to contain null Source to denote "not found".
- Adds default json based mapper.
Added
*****
- Add skip search request parameter to "skip" documents in the result.
- Add Total property to search response 
- Supports MatchAll filters
- Addd Helper function to easily map json.

0.1.0.11
-------
Changed
*******
- Deterministic build

0.1.0.10
--------
Added
*****
- Added ServiceSearchEngine dependency to query the search system and IServiceSearchProvider to write search providers.
- Added Lucene SearchProvider and ways for apps to create Lucene indices, index documents in Lucene and provide storage for actual doc contents.
