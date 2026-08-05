# Changelog

## 3.0.0 - framework release

AVAK becomes an extensible toolkit. The shell and scan loop stay; what counts as
malicious is now plugin- and data-driven.

**Extension points**
- `AVAK.Engine.IDetectionProvider` - one interface, no registration code. Public
  types in any DLL under `plugins\` are discovered at startup, appear on the new
  Rules page and can be switched on and off there.
- The four built-in detectors (hash, pattern, archive, heuristic) were rewritten as
  providers, so they use the same contract third-party code does.
- `DetectionContext` gives providers lazily computed facts about a file: the 2 MB
  head buffer, SHA-256 and MD5 are each computed at most once no matter how many
  providers ask.
- A provider that throws is isolated and disabled after five failures instead of
  breaking the scan; the reason is shown in the UI.

**Data-driven content**
- Rule packs: any number of JSON files under `content/rules` (shipped, read-only)
  and `%LOCALAPPDATA%/AVAK/rules` (yours). Each pack carries metadata, can be
  enabled or disabled individually, and reports which pack a detection came from.
- `heuristics.json` exposes every heuristic weight, threshold, extension list, API
  name and script marker. A user copy overrides the shipped file.
- Pack validation catches short patterns, malformed hex, wrong hash lengths,
  duplicates and extensions missing their dot.
- The old single `data\signatures.json` is migrated into a rule pack on first run.

**Authoring**
- New Rules page: browse providers and packs, enable/disable, add a hash from a
  file, add a pattern, validate everything, test the rules against a folder, and
  open the heuristics file in an editor.
- `--rules` CLI with `list`, `new`, `add-hash`, `add-pattern`, `remove`,
  `validate`, `test` and `import`, so rules can be maintained from a script or a
  CI job. `--rules test` returns the detection count as its exit code.

**Removed**
- The commercial licensing layer (signed keys, trial, feature gates) and the EULA
  and commercialization documents. Feature gating is incompatible with a framework
  anyone is meant to build on. Everything is enabled for everyone.

**Also**
- `--uitest` now derives its page list from the registered views, so a new page is
  covered by the self-test the moment it is registered.
- JSON is written in camelCase, matching the documented rule-pack format.
- New docs: ARCHITECTURE, EXTENDING, RULES, CONTRIBUTING, plus a buildable sample
  plugin under `samples\SamplePlugin`.
- 82 unit tests, up from 59.

## 2.1.0

**Detection**
- Real archive scanning. ZIP, JAR, APK and the OOXML office formats are opened
  and every entry is hashed, pattern-matched and heuristically analysed. Guarded
  against decompression bombs (per-entry cap, total cap, compression-ratio check,
  nesting depth limit) and Zip Slip entry names. The *Look inside archives*
  toggle previously changed a setting the engine never read.
- Wildcard exclusions. `*.iso` and `C:\Builds\*\bin\*` now work; plain entries
  still match by prefix.

**Correctness**
- Fixed a licence-encoder bug where a VB inclusive array bound added a stray
  trailing byte, so signatures could never verify. Found by a unit test.
- Real-time counters are now read and written with `Interlocked`; they were
  plain fields mutated from the watcher thread.
- The privacy cleaner skips files written in the last 90 seconds, so it no longer
  fights with a running installer.

**Product**
- Offline licensing: ECDSA P-256 signed keys, Base32 rendering, 30-day trial,
  feature gates, activation UI. The open-source build ships with no vendor key,
  which puts it in Community mode with everything unlocked.
- "Scan with AVAK" in the Explorer right-click menu, registered per-user. A
  second instance hands its path to the running one instead of refusing to start.
- Optional update check against a feed URL you configure. Empty by default —
  AVAK still makes no network request on its own.
- Sound feedback on detection (Windows sound scheme) and a *Confirm before
  deleting* preference. Both settings existed but were never read.
- Network adapters table and *Remove AVAK rules* button — both were implemented
  and unreachable from the UI.

**Shipping**
- 59 unit tests (`dotnet test`) covering the glob matcher, signature engine,
  heuristics, archive scanner, palette, theme maths, every icon, the SVG path
  parser, formatting, licensing and the JSON store.
- `publish.bat` produces a self-contained single-file x64 build, refusing to
  publish if the tests fail or the published binary fails its self-test.
- Inno Setup installer script, per-user by default, with an uninstall prompt
  about keeping quarantine and settings.
- Real multi-resolution application icon generated from the vector shield.
- EULA, privacy statement, third-party notices and an honest commercialization
  checklist.

## 2.0.0

Complete rewrite. VB.NET on .NET 8, SDK-style project, UI built in code with a
Catppuccin theme. Replaces the .NET Framework 4.5 mockup, which had no working
features behind its interface.

- Scan engine: SHA-256/MD5 signatures, wildcard byte patterns, PE structure and
  entropy analysis, script obfuscation heuristics, four scan profiles
- AES-256 quarantine with DPAPI-protected keys
- Real-time protection via FileSystemWatcher
- Firewall control, TCP connection table, Windows VPN profile management
- Performance counters, hardware inventory, process list with Authenticode
  verification, startup manager
- Privacy cleanup, Microsoft Defender status, hosts-file audit
- Scan history, activity feed, scheduled scans, tray integration, local profile
  with PBKDF2 PIN lock
- `--scan`, `--selftest` and `--uitest` command-line modes
