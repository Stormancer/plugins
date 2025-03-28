=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

2.1.4
----------
Changed
*******
- Update dependencies to release version

2.1.3
----------
Added
*****
- Added GetOptions() method that returns an IOptions<T> to IConfiguration.
- Added TryGetValue() method to IConfiguration to check if a configuration section exists when retrieving its value

2.1.2.5
----------
- Update dependency to Stormancer.Abstraction.Server 9.0.0

2.1.2.2
-------
Changed
*******
- Deterministic build

2.1.2.1
-------
Changed
*******
- Use Stormancer.Abstraction.Server 5.0.0

2.1.2
-----
- Use Stormancer.Abstraction.Server 4.1.0

2.1.1.1
----------
Added
*****
- Added Licence expression to nuget package.

2.1.1
-----
Changed
*******
- Make sure that IConfigurationChangedEventHandler cannot be called twice. However, take note that 'Single instance per scene' dependencies implementing IConfigurationChangedEventHandler declared in the host container will be instantiated in ALL running scenes. Dependencies expected to be created in some scenes only should therefore be declared in the scene containers.
2.1.0
-----
Changed
*******
- IConfigurationChangedEventHandler.OnDeploymentChanged now taks a DeploymentChangedEventArgs argument.
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

