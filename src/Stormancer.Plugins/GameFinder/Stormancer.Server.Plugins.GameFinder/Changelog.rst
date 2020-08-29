=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

Unreleased
----------
- Removed support for direct game search request from client. All gamesearch request should now be done through a party.
- Advertise party and party member settings in game finder
- Removed IDataExtractor from pipeline
- Renamed IGameFinder into IGameFinderAlgorithm
- Added QuickQueue gamefinder algorithm with configuration extension methods.
- Easier gamefinder configuration.
Added
*****

- Open Game Sessions feature: Allows opening existing game sessions to new players on the GameFinder.

Changed
*******

- Automatic increment of party passes (PastPasses)
