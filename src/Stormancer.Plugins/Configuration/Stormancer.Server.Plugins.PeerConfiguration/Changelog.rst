=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

0.2.1.1
----------
Changed
*******
- Don't send config updates if it didn't change.
Added
*****
- Added a readme file.

0.1.0.7
----------
- Update dependency to Stormancer.Abstraction.Server 9.0.0

0.1.0.3
-------
Changed
*******
- Deterministic build

0.1.0.2
-------
Added
*****
- Initial release of the PeerConfiguration plugin. The plugin synchronizes a configuration from a subsection of the SA config to client peers, with the possibility of grouping the peers into group and sending different configurations to each groups.
