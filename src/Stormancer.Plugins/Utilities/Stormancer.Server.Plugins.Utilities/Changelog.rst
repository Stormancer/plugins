=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

3.2.0.14
----------
Added
*****
- Added Result<TResult,TError> utility type.
- Added Result<TError> utility type for exceptionless results.
- Added RecyclableMemoryStreamProvider class.

Fixed
*****
- Fixed bug in invalidation logic for cache entries created using the overload with an invalidation delay as argument.

3.1.4.2
-------
Fixed
*****
- When addfunction throws an exception, the cache should immediately evict the value.
- Fixed threading issue in MemoryCache

3.1.2
-----
Changed
*******
- Use PeriodicTimer to manage invalidation of cache entries.

3.1.1.1
----------
- Update dependency to Stormancer.Abstraction.Server 9.0.0

Added
*****
- Added Task.WaitAsync extension methods to enable "cancellation" of non cancellable tasks. In case of cancellation, the source task keeps running, but the resultant task faults.
- Remove PipeReaderExtensions class because it's now included in Stormancer.Abstractions.Server.

2.3.0.12
--------
Changed
*******
- Deterministic build

2.3.0.11
--------
Added
*****
- Added MemoryCache<T>.GetMany batched cache access function.

2.2.0
-----
Added
*****
- MemoryCache: Added a Get overload that supports providing a cache entry invalidation delay as a return of the cache value getter.
- MemoryCache: Add nullable annotations.
2.1.0.6
-------
Added
******
- Added IRemotePipe interface implemented by S2SOperation.
- Added Extension methods for copying pipes.

Changed
*******
- Use Stormancer.Abstraction.Server 5.0.0

2.0.4.1
-------
Added
*****
- Add S2SOperation classes.

2.0.3.1
-------
Changed
*******
- Removed cleanup cycle trace logs.

2.0.3
-----
Changed
*******
- Use Stormancer.Abstraction.Server 6.1.0

2.0.2
-----
Changed
*******
- Set Apache 2.0 License because this package contains some modified code under Apache2.0 (Modified HttpClientFactory implementation.)

2.0.0.1
----------
Changed
*******
- Update to .NET5.0

1.3.0
-----
Added
*****

Changed
*******
- New versioning system

Removed
*******

