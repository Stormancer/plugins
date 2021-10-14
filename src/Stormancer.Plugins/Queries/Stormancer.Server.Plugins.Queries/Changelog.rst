=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

0.1.0.10
--------
Added
*****
- Added ServiceSearchEngine dependency to query the search system and IServiceSearchProvider to write search providers.
- Added Lucene SearchProvider and ways for apps to create Lucene indices, index documents in Lucene and provide storage for actual doc contents.
