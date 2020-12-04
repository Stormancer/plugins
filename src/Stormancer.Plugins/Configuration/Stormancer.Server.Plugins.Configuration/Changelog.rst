=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

2.0.0.2
----------
- Update to .NET5.0

1.2.1.2
-------
Added
*****
- Support default configuration API.

Changed
*******
- BREAKING: Implement IConfigurationChangedEventHandler to listen to ConfigurationChanged and DeploymentChanged events. 
The previous way of doing this (subscribing to events) could lead to memory leaks and performance degradation.

Removed
*******
- IConfiguration.SettingsChanged was removed because of the difficulty to use it correctly and the performance issues bad usage could lead to.

