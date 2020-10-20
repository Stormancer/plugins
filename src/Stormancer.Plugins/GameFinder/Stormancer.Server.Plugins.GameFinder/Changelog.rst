=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

3.0.0.16
--------
Added
*****
- Added QuickQueue gamefinder algorithm with configuration extension methods.
- Open Game Sessions feature: Allows opening existing game sessions to new players on the GameFinder.
- Easier gamefinder configuration.
- Advertise party and party member settings in game finder algorithm
- Add AddGameFinder extension method to IHost to easily add & configure gamefinders in an application.

Changed
*******
- Renamed IGameFinder into IGameFinderAlgorithm
- Renamed GameFinderContext.WaitingClient into GameFinderContext.WaitingParties
- Automatic increment of party passes (PastPasses)

Removed
*******
- Removed IDataExtractor from pipeline
- Removed support for direct game search request from client. All gamesearch request should now be done through a party.
