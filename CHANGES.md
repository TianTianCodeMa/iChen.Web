Release 4.3
===========

New Features
------------

- Add support for HTTP port redirection.

- Add support for HTTP/2 for HTTPS clients.

- Add Brotli compression for clients that support it.
  _(.NET Core only)_

Enhancements
------------

- Use ASP.NET Core cookies authentication middleware.

Refactoring
-----------

- Refactor code for ASP.NET Core startup and configuration.

- Do not reuse database context.

Breaking Changes
----------------

- Requires `iChen.OpenProtocol.dll` version 4.3 and up.

- Requires `iChen.Analytics.dll` version 4.3 and up.

- Requires `iChen.Persistence.Config.dll` version 4.3 and up.

- Requires `iChen.Persistence.Cloud.dll` version 4.3 and up.



Release 4.2
===========

New Features
------------

- HTTPS is enabled on Kestrel with support for HSTS.

Breaking Changes
----------------

- `Type` field in `ControllerX` type is changed to numeric.
  A new `enum` `KnownControllerTypes` is added to map common
  controller types to their respective numeric ID's.

- Requires `iChen.OpenProtocol.dll` version 4.2 and up.

- Requires `iChen.Analytics.dll` version 4.2 and up.

- Requires `iChen.Persistence.Config.dll` version 4.2 and up.

- Requires `iChen.Persistence.Cloud.dll` version 4.2 and up.


Release 4.1.1
=============

New Features
------------

- Added support for `COM` (Windows) and `tty` (Linux) style serial ports.
