# Third-party components

## DDrawCompat

* Project: <https://github.com/narzoul/DDrawCompat> by narzoul
* Version used: **v0.7.2**
* Not part of this repository. The patcher downloads
  `https://github.com/narzoul/DDrawCompat/releases/download/v0.7.2/DDrawCompat-v0.7.2.zip`
  and verifies the SHA-256 of the archive and of the contained `ddraw.dll` before installing it.
  * archive: `A932A4DCB2100552B6918FD4A3CC7DE138BB97448BBCA2A7051954993A397B39`
  * `ddraw.dll`: `94895CF6EDE8FD0F74EE1447FDC08B7044A9C65A1A25A49A4DF893A0370975A3`
* License: see the license of the DDrawCompat project (the upstream repository states the terms of its release).

## Windows components

The patcher uses `cabinet.dll` (FDI API) and `csc.exe` of the .NET Framework 4 that ship with Windows. 7-Zip (optional, only for unpacking `.img` / `.iso`) is not included.
