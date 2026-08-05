# Contributing

AVAK is a framework. The most valuable contributions are usually rule packs and
detection providers, not changes to the core.

## Before you write code

Check whether you need to. Three extension points cover most ideas without
touching the repository at all:

| You want to... | Do this instead |
|---|---|
| Detect specific files | Add a rule pack - [docs/RULES.md](docs/RULES.md) |
| Change how aggressive heuristics are | Edit `heuristics.json` |
| Detect something in a new way | Write a plugin - [docs/EXTENDING.md](docs/EXTENDING.md) |
| Add a page to the UI | Subclass `ViewBase` and register it in `MainForm` |

## Building

```bat
dotnet build AVAK.sln -c Release
dotnet test
```

Requires the .NET 8 SDK or newer. Windows only - the project targets
`net8.0-windows` and uses Win32 APIs throughout.

## Definition of done

A change is ready when all four pass:

```bat
dotnet build AVAK.sln -c Release      rem 0 errors, 0 warnings
dotnet test                           rem all green
AVAK.exe --selftest                   rem exit code 0
AVAK.exe --uitest .\shots             rem exit code 0
```

`--uitest` builds every page and renders it to a PNG. It has caught real
regressions that a compile could not: a null reference during construction, a
docking order mistake that hid the sidebar, buttons overflowing their card. Look
at the PNGs, not just the exit code.

## VB.NET traps in this codebase

These have each cost real debugging time here. Worth knowing before your first
build failure:

**Identifiers are case-insensitive**, so a local shadows a type with the same
name. `Dim path = ...` breaks every later `Path.Combine`. The same trap applies to
`file`, `list`, `tone`, `aes`, `md5`, `len`, `loc`, `json` and `hit`. Name locals
`filePath`, `items`, `target`.

**`Byte - Byte` stays a Byte** and throws `OverflowException` when the result is
negative. Widen with `CInt()` first. This is what broke the colour mixer, and
there is a regression test for it.

**`Dim a(n)` allocates n+1 elements.** Array bounds are inclusive. This produced a
one-byte-too-long buffer that silently broke signature verification until a unit
test caught it.

**`Handles`, `Variant`, `Len`, `Loc` are keywords** and cannot be identifiers.

**`Security.Cryptography` resolves to `AVAK.Security`** inside this root
namespace. Write `System.Security.Cryptography`.

**`Timer` is ambiguous** when a file imports both `System.Threading` and
`System.Windows.Forms`. Write `Global.System.Windows.Forms.Timer`.

## Style

- `Option Strict On` everywhere. No exceptions.
- Comments explain *why*. The code already says what.
- Files under ~800 lines; methods under ~50.
- No new hard-coded detection data - it belongs in `content/`.
- UI is built in code (`Build()` + `Relayout()`), not in the designer.
- Colours come from `ThemeManager`, never a literal RGB.

## Rule pack contributions

Wanted, with conditions:

- Say where the data came from and under what licence.
- No copied vendor signature databases.
- No malware samples in the repository - hashes and behaviour patterns only.
- Include the false-positive result: which clean corpus you tested against and
  what came back. A pack that fires on Windows itself will not be merged.
- Prefix uncertain names with `HEUR:` or `Suspicious.`.

## Reporting a false positive

Open an issue with the detection name, the file's SHA-256, where the file came
from and which pack fired (shown in the detection details). False positives are
treated as bugs, not as tuning requests.

## Security issues

Do not open a public issue for a vulnerability in AVAK itself - report it
privately first.

Note what AVAK deliberately does not defend against: plugins run in-process with
full rights, and quarantine protects against accidental execution, not against an
attacker who already controls your Windows session. Both are documented; neither
is a vulnerability.

## Licence

Contributions are MIT, same as the project.
