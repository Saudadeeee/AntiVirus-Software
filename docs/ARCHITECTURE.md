# Architecture

AVAK is a Windows security toolkit built as a framework: the shell, the scan loop
and the UI are fixed, and almost everything that decides *what is malicious* is
either a plugin or a JSON file.

```
                      ┌────────────────────────────┐
   folders / files ──▶│         ScanEngine         │  enumerate, parallelise,
                      │  (Security/ScanEngine.vb)  │  progress, cancellation
                      └─────────────┬──────────────┘
                                    │ one DetectionContext per file
                                    ▼
                      ┌────────────────────────────┐
                      │      ProviderRegistry      │  ordering, health,
                      │   (Engine/Registry.vb)     │  enable/disable, plugins
                      └─────────────┬──────────────┘
                    ┌───────────────┼───────────────┬──────────────┐
                    ▼               ▼               ▼              ▼
              HashProvider   PatternProvider  ArchiveProvider  your plugin
                 (100)            (200)            (300)         (850)
                    │               │               │              │
                    └───────────────┴───────┬───────┴──────────────┘
                                            ▼
                                    ┌───────────────┐
                                    │   Detection   │ ─▶ quarantine / report / UI
                                    └───────────────┘
```

## The two extension points

**1. Detection providers (code).** A provider answers one question: "is this file
bad?". Implement `AVAK.Engine.IDetectionProvider`, drop the DLL in `plugins\`, and
it appears on the Rules page. See [EXTENDING.md](EXTENDING.md).

**2. Rule packs and profiles (data).** JSON files that need no compiler:

| File | What it controls |
|---|---|
| `content/rules/*.json` | Hash signatures and byte patterns (shipped, read-only) |
| `%LOCALAPPDATA%\AVAK\rules\*.json` | Your own packs (writable) |
| `content/heuristics.json` | Every heuristic weight, threshold, API name and script marker |
| `%LOCALAPPDATA%\AVAK\heuristics.json` | Your override, wins over the shipped file |

See [RULES.md](RULES.md) for both formats.

## Layers

```
src/Engine/      the extension surface
                   IDetectionProvider  the contract you implement
                   DetectionContext    lazily computed facts about one file
                   ProviderRegistry    discovery, ordering, health, plugin loading
                   Providers/          the four built-in providers

src/Content/     data-driven content
                   RulePack            one JSON rule file + validation
                   RuleStore           all packs: index, enable, edit, hot reload
                   HeuristicProfile    weights and markers loaded from JSON
                   RuleCli             --rules verbs for scripting

src/Security/    the engine
                   ScanEngine          enumeration, parallel scan, cancellation
                   SignatureDatabase   pattern compiler, matcher, file hashing
                   HeuristicAnalyzer   PE, entropy, scripts - all profile-driven
                   ArchiveScanner      ZIP-family containers, bomb guards
                   QuarantineManager   AES vault, DPAPI keys, restore, shred
                   RealtimeMonitor     FileSystemWatcher + debounce
                   ScanScheduler       daily/weekly runs
                   HistoryStore        scan reports and the activity feed

src/Core/        infrastructure: paths, logging, JSON, settings, elevation,
                 local profile, shell integration, update check
src/SystemInfo/  performance counters, hardware, processes, autoruns
src/Network/     firewall, TCP table, VPN profiles
src/Privacy/     cleanup targets, Defender status, hosts audit
src/Theme/       Catppuccin palettes + ThemeManager
src/Ui/          SVG path parser, vector icons, custom controls, dialogs
src/Views/       one file per page; add a page by subclassing ViewBase
src/Forms/       the shell window
```

## Scan flow, precisely

1. `ScanEngine.ScanAsync` enumerates targets for the profile, skipping excluded
   paths and system folders, and reports a file count.
2. `Parallel.ForEach` runs `InspectFile` across `EffectiveThreads()` workers.
3. `InspectFile` applies the exclusion rules, then builds one
   `DetectionContext` and hands it to `ProviderRegistry.Inspect`.
4. The registry walks enabled providers in `Order`:
   - `CanHandle(context)` first - a cheap filter that must not read the file.
   - `Inspect(context)` only if that passed.
   - A **terminal** provider that returns a detection ends the chain.
   - A non-terminal provider's detection is kept only if it is the most severe.
   - A provider that throws is counted; after 5 failures it is disabled and the
     reason is shown on the Rules page. One bad plugin cannot break a scan.
5. Detections flow back to the UI, quarantine, history and the activity feed.

## DetectionContext is lazy on purpose

A context computes nothing until asked:

| Property | Cost |
|---|---|
| `Extension`, `FileName`, `SizeBytes` | free |
| `Buffer` / `BufferLength` | one read of up to 2 MB, once per file |
| `Sha256` | full file read, once per file |
| `Md5` | full file read, only if something asks |
| `IsPortableExecutable` | uses the buffer |

So a provider that only looks at the extension costs nothing, and ten providers
that all need the head of the file still cause exactly one read.

## Threading

- `Inspect` runs on many threads at once. Keep providers re-entrant.
- `DetectionContext` belongs to one thread; the registry creates one per file.
- `RuleStore` and `ProviderRegistry` guard their state with a lock and hand out
  snapshots, so a reload during a scan cannot corrupt anything.
- Realtime counters use `Interlocked`.
- UI updates from workers go through `ViewBase.Ui_(...)`.

## Adding a whole page

A page is a `ViewBase` subclass with a `Build()` and a `Relayout()`. Register it
in `MainForm.BuildViews` and add an entry to the sidebar list in `BuildChrome`.
`AVAK.exe --uitest <folder>` then renders it to a PNG along with the others, so a
layout mistake fails with an exit code instead of at runtime.

## What is deliberately not extensible

Quarantine encryption, the settings schema and the theme engine are fixed. They
are correctness- and safety-critical, and a pluggable crypto layer would be a
liability rather than a feature.
