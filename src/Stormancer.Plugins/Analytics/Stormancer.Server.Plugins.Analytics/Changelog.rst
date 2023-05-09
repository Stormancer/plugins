=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

2.0.6.1
----------
Changed
*******
- Added ServerError property to Errors log when analytics couldn't be indexed in Elastic.

Added
*****
- Configure a scene to accept analytics from clients.

2.0.6
-----
Changed
*******
- Improve performance by removing async state machine if instrumentation is disabled.

2.0.5
-----
- Separates instrumentation for S2S and instrumentation for client APIs in configuration
- Stringifies session ids in metrics

2.0.4.5
----------
- Remove dependency to Stormancer.Abstraction.Server 

2.0.4.1
-------
Changed
*******
- Deterministic build

2.0.4
-----
Changed
*******
- (Breaking) AnalyticsController.Push now is a Fire & Forget client API instead of an RPC. 

2.0.3.1
-------
Changed
*******
- Target Final Stormancer.Abstraction 5.0.0

2.0.3
-----
Changed
*******
- Updated Api dependency;
Added
*****
- API metrics for new S2S protocol.

2.0.2
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.2.0

2.0.1
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

2.0.0.3
----------
Changed
*******
- Update to .NET5.0
Added
*****
- Set "instrumentation.EnableApiInstrumentation" to true in config to enable saving analytics about API usage & performance. (disabled by default)

1.0.1
-----
Added
*****

Changed
*******
- Update script build

Removed
*******

