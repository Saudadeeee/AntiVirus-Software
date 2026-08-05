# Third-party notices

AVAK is distributed with the components below. Their licences govern those
components. Nothing here is bundled in source form; all are NuGet packages
restored at build time or part of the .NET runtime.

---

## Microsoft .NET 8 runtime and libraries

Copyright (c) .NET Foundation and Contributors
Licensed under the MIT License — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

Includes the Windows Desktop runtime (Windows Forms, System.Drawing.Common),
`System.Text.Json`, `System.IO.Compression` and `System.Security.Cryptography`.

Self-contained builds produced by `publish.bat` redistribute the .NET runtime,
which the MIT licence permits.

## System.Management

Copyright (c) .NET Foundation and Contributors
Licensed under the MIT License — https://www.nuget.org/packages/System.Management

Used for WMI queries: hardware inventory, Microsoft Defender status.

## System.Diagnostics.PerformanceCounter

Copyright (c) .NET Foundation and Contributors
Licensed under the MIT License — https://www.nuget.org/packages/System.Diagnostics.PerformanceCounter

Used for live CPU, disk and network counters.

## System.Security.Cryptography.ProtectedData

Copyright (c) .NET Foundation and Contributors
Licensed under the MIT License — https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData

Used to protect per-item quarantine encryption keys with Windows DPAPI.

## MSTest (test project only — not redistributed)

Copyright (c) Microsoft Corporation
Licensed under the MIT License — https://github.com/microsoft/testfx/blob/main/LICENSE.txt

---

## Design credits

### Catppuccin colour palette

Copyright (c) 2021 Catppuccin
Licensed under the MIT License — https://github.com/catppuccin/catppuccin/blob/main/LICENSE

The Mocha, Macchiato, Frappé and Latte palettes are reproduced as colour values
in `src/Theme/Palette.vb`. Colour values themselves are not copyrightable in
most jurisdictions, but the palette is credited here because it is the work of
the Catppuccin project and deserves attribution.

### Icons

The icon set in `src/Ui/Icons.vb` is hand-authored geometry written for this
project. It is stylistically inspired by [Lucide](https://lucide.dev) (ISC
licence) — same 24×24 grid and 2px round-capped stroke — but no Lucide path data
was copied. The icons are covered by AVAK's own licence.

---

## Detection data

`data/signatures.json` contains:

- The **EICAR** anti-malware test file hash and string. EICAR publishes this
  specifically so antivirus products can be tested; see https://www.eicar.org.
- The **GTUBE** anti-spam test string, published by SpamAssassin for the same
  purpose.
- Behavioural patterns describing well-known destructive commands
  (`vssadmin delete shadows`, `wbadmin delete catalog`, and similar). These are
  descriptions of technique, not copied malware code.

No malware samples or third-party vendor signature databases are included or
derived from.

---

## Fonts

AVAK uses whichever of these is installed on the user's system, in order:
Segoe UI Variable Text, Segoe UI, Inter, Noto Sans, Tahoma, Arial. No font files
are redistributed.
