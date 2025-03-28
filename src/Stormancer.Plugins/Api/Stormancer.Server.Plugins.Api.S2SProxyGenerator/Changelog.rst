=========
Changelog
=========

All notable changes to this project will be documented in this file.

The format is based on `Keep a Changelog <https://keepachangelog.com/en/1.0.0/>`_, except reStructuredText is used instead of Markdown.
Please use only reStructuredText in this file, no Markdown!

This project adheres to semantic versioning.

1.1.0.4
----------
Changed
*******
- Update dependencies to release version

1.1.0.3
----------
Fixed
*****
- Complete the S2S request writer after writing the arguments if the writer is not needed by the caller to prevent a soft lock if there are no arguments.

1.0.3.6
----------
Changed
*******
- Generated code is now in the project's namespace.

1.0.3.1
-------
Changed
*******
- Deterministic build

1.0.3
-----
Fixed
*****
- Add 'using Stormancer.Server' at the top of generated files.

1.0.2.4
-------
Changed
*******
- Ensure nuget package is an analyzer package.

1.0.1
-----
Changed
*******
- Don't generate parameters in S2S proxies for CancellationToken.

1.0.0
-----
Added
*****
- Added code generator for S2S actions.
