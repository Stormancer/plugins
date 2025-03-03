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

1.0.0.10
----------
Added
*****
- initial Collections release
- Unlock, lock and reset items in the collection.

Fixed
*****
- GetCollectionsAsync should always return an entry per provided user id, even if no unlocked items were found for this entry.
