=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

Unreleased
----------
Added
*****
- Added Admin web apis to retrieve bug reports and bug reports attachments.

Changed
*******
- Removed argument to send custom attachments from the bug report API. It should be handled through the independant blob upload API.

0.1.0.33
----------
Added
*****
- Initial implementation of the player reporting service.
