# Rule packs and heuristics

Everything AVAK detects without a plugin comes from JSON files you can edit.
No compiler, no rebuild.

---

## Where files live

| Folder | Contents |
|---|---|
| `<AVAK folder>\content\rules\` | Shipped packs. Read-only; disable rather than edit. |
| `%LOCALAPPDATA%\AVAK\rules\` | Your packs. Created by the UI, the CLI or by hand. |
| `<AVAK folder>\content\heuristics.json` | Shipped heuristic weights. |
| `%LOCALAPPDATA%\AVAK\heuristics.json` | Your override. Wins over the shipped file. |

Any `*.json` in a rules folder is loaded. The `.rules.json` suffix is a convention,
not a requirement.

---

## Rule pack format

```json
{
  "name": "My rules",
  "version": "2026.08.05",
  "author": "you",
  "description": "What this pack is for",
  "license": "MIT",
  "homepage": "https://example.com",
  "updated": "2026-08-05T00:00:00",

  "hashes": [
    {
      "sha256": "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f",
      "md5": "44d88612fea8a8f36de82e1278abb02f",
      "name": "EICAR-Test-File",
      "severity": "High"
    }
  ],

  "patterns": [
    {
      "name": "Suspicious.Script.ShadowCopyWipe",
      "ascii": "vssadmin delete shadows /all /quiet",
      "severity": "Critical",
      "maxOffset": 0,
      "extensions": [".bat", ".cmd", ".ps1"]
    },
    {
      "name": "Example.DosStub",
      "hex": "4D5A??00 0300",
      "severity": "Low",
      "maxOffset": 64
    }
  ]
}
```

Only `name` is required at the top level. `hashes` and `patterns` may each be
empty or absent.

### Hash entries

| Field | Required | Notes |
|---|---|---|
| `sha256` | one of the two | Exactly 64 hex characters |
| `md5` | one of the two | Exactly 32 hex characters. Only compute MD5 when you must - it makes every scan read the file twice. |
| `name` | yes | The threat name shown to the user |
| `severity` | no | `Low`, `Medium`, `High` (default), `Critical` |

### Pattern entries

| Field | Required | Notes |
|---|---|---|
| `name` | yes | Threat name |
| `ascii` | one of the two | Literal text. Minimum 4 characters. |
| `hex` | one of the two | Byte pattern; `??` is a wildcard byte. Spaces and dashes are ignored. |
| `severity` | no | Default `High` |
| `maxOffset` | no | Search only the first N bytes. `0` searches the whole 2 MB window. Use it for magic numbers - it is much faster. |
| `extensions` | no | Restrict to these extensions, with the dot. Empty means every file. |

Patterns are searched inside the first 2 MB of a file and inside every archive
entry.

### Severity, and what it does

| Severity | Effect |
|---|---|
| `Low` | Recorded, not surfaced as a threat |
| `Medium` | Reported; eligible for auto-quarantine |
| `High` | Reported prominently |
| `Critical` | Reported prominently; drives the dashboard score |

---

## Adding rules

### From the UI

**Rules → Rule packs**. *Add hash from file* fingerprints a file you already have.
*Add pattern* asks for the text, the extensions and the severity. *New pack*
creates an empty writable pack. Double-click a row to enable or disable a pack.

### From the command line

```bat
AVAK.exe --rules list
AVAK.exe --rules new --pack "Office macros"

AVAK.exe --rules add-hash --file suspicious.exe --name Trojan.Example --severity High
AVAK.exe --rules add-hash --sha256 275a021b... --name EICAR-Test --pack "Office macros"

AVAK.exe --rules add-pattern --ascii "powershell -enc" --name My.EncodedPs --ext .bat,.cmd
AVAK.exe --rules add-pattern --hex "4D5A??00" --name My.PeStub --max-offset 64

AVAK.exe --rules remove --name My.PeStub --pack office-macros.rules.json
AVAK.exe --rules import downloaded.json
AVAK.exe --rules validate
AVAK.exe --rules test C:\corpus
```

`--pack` takes a name or a file; an unknown name creates the pack. Output goes to
`rules-cli.txt` next to the executable, because AVAK is a windowed application
with no console of its own. Exit codes: `0` success, `1` failure, `2` bad usage.
`--rules test` returns the number of detections, which makes it usable as a CI
gate.

### By hand

Write the JSON, drop it in `%LOCALAPPDATA%\AVAK\rules\`, press **Reload plugins**
or restart. Run `--rules validate` first - it catches short patterns, malformed
hex, wrong hash lengths, duplicates and extensions missing their dot.

---

## Generating a pack from a feed

Rule packs are ordinary JSON, so any script can produce one. Example against a
public hash list:

```python
import json, urllib.request

rows = urllib.request.urlopen("https://example.com/hashes.csv").read().decode()

pack = {
    "name": "Imported feed",
    "version": "2026.08.05",
    "author": "feed importer",
    "license": "check the feed's own licence",
    "hashes": [],
    "patterns": [],
}

for line in rows.splitlines():
    sha, family = line.split(",")[:2]
    if len(sha) == 64:
        pack["hashes"].append(
            {"sha256": sha, "name": family or "Feed.Unknown", "severity": "High"})

with open("feed.rules.json", "w", encoding="utf-8") as f:
    json.dump(pack, f, indent=2)
```

Then `AVAK.exe --rules import feed.rules.json`.

**Practical limit.** The store keeps hashes in a dictionary, so lookup stays O(1),
but the JSON is parsed at startup. Tens of thousands of hashes are fine; if you
want to import a million-entry feed, split it across packs or add a provider that
reads a binary index instead - that is exactly what the plugin interface is for.

---

## heuristics.json

Every number the heuristic engine uses. Copy the shipped file to
`%LOCALAPPDATA%\AVAK\heuristics.json` (Rules → **Edit heuristics** does it for
you) and your copy wins.

```jsonc
{
  "thresholds":  { "low": 18, "medium": 32, "high": 50, "critical": 70 },
  "sensitivityMultipliers": { "lenient": 0.75, "balanced": 1.0, "aggressive": 1.25 },

  "extensions": {
    "executable": [".exe", ".dll", "..."],
    "script":     [".ps1", ".bat", "..."],
    "document":   [".docm", ".xlsm", "..."],
    "lure":       [".pdf", ".jpg", "..."]     // second extension in "x.pdf.exe"
  },

  "structural": {
    "doubleExtension": 34,                    // every weight is tunable
    "writableExecutableSection": 20,
    "veryHighEntropy": 20
  },

  "entropy": { "veryHigh": 7.6, "high": 7.2, "packedLabel": 7.4 },

  "riskyApis":     [ { "name": "CreateRemoteThread", "weight": 14 } ],
  "scriptMarkers": [ { "needle": "invoke-expression", "weight": 18,
                       "why": "Invoke-Expression" } ]
}
```

A file scores by adding weights, is multiplied by the sensitivity setting, and is
graded against `thresholds`.

**Tuning honestly.** Raising weights raises detection *and* false positives. After
any change, run `--rules test` over a folder you know is clean. If that produces
hits, the change made things worse, whatever it did to detection.

---

## Naming detections

The built-ins follow a loose convention that keeps lists readable:

```
EICAR-Test-File                     exact, well-known sample
Suspicious.Script.ShadowCopyWipe    <confidence>.<platform>.<behaviour>
HEUR:Win32/Packed.Gen               heuristic, not an exact identification
Exploit:Archive/PathTraversal       technique-based
```

Prefix a guess with `HEUR:` or `Suspicious.`. Reserve a bare family name for
something you actually identified.
