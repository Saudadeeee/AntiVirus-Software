# AVAK privacy statement

**Short version: AVAK does not send your data anywhere. There is no account
server, no telemetry, no analytics and no crash reporting service.**

This statement describes what the software actually does, verifiable by reading
the source. Every network call in the codebase is listed below.

---

## What AVAK stores, and where

Everything lives under `%LOCALAPPDATA%\AVAK` on your own computer.

| File / folder | Contents |
|---|---|
| `settings.json` | Your preferences: theme, exclusions, schedule, behaviour |
| `profile.json` | Display name, avatar colour, PBKDF2 hash of your PIN |
| `license.json` | Your licence key and the trial start date |
| `data/scan-history.json` | Scan reports: paths scanned, detections, timings |
| `data/events.json` | The activity feed shown on the dashboard |
| `data/quarantine-index.json` | Metadata for quarantined files, including their original paths |
| `data/signatures.json` | Your copy of the detection database |
| `quarantine/*.avq` | AES-256 encrypted copies of files AVAK quarantined |
| `logs/*.log` | Rolling diagnostic logs; these contain file paths |

Deleting that folder erases everything AVAK knows.

## Your PIN

If you set an app PIN, AVAK stores only a PBKDF2-SHA256 hash (120,000
iterations, per-profile random salt). The PIN itself is never written to disk
and cannot be recovered from the hash.

The PIN gates the AVAK window. It is not disk encryption and does not protect
your files from anyone with access to your Windows account.

## Quarantine encryption

Quarantined files are encrypted with AES-256-CBC using a random key per item.
That key is then protected with the Windows Data Protection API (DPAPI) scoped
to your Windows user account. A different Windows user, or the same user on a
different machine, cannot decrypt your quarantine.

This protects against accidental execution and casual inspection. It is not a
defence against an attacker who already controls your Windows session.

## Every network request AVAK can make

AVAK makes **no** network request unless you take an explicit action:

1. **Signature update** — only when you paste a URL in
   *Settings → Signature database* and press *Update from URL*. AVAK sends a
   plain HTTPS GET and receives a JSON file. Nothing about your system is sent.

2. **Application update check** — only when you configure an update feed URL in
   *Settings → Integration & updates*. AVAK sends a GET with a
   `User-Agent: AVAK/<version>` header. The version number is the only thing
   disclosed; it is disclosed to a server **you** chose.

3. **Opening a link** — pressing a *Download* or *Open* button hands a URL to
   your default browser. From that point your browser's privacy applies.

Both feed URLs are empty by default. With the default settings AVAK never opens
a socket.

## What AVAK reads on your computer

To do its job, AVAK reads:

- files you ask it to scan, plus the folders you add to real-time protection
- the running process list and the on-disk path of each process
- Windows Firewall state, network adapters and the TCP connection table
- registry autorun keys and both Startup folders
- Microsoft Defender status via WMI
- the `hosts` file
- hardware and OS information via WMI and the registry

All of this stays on the machine. None of it is transmitted or aggregated.

## Licence keys

Licence verification is entirely offline. A key is a signed blob that AVAK
verifies against a public key compiled into the binary. No activation server is
contacted, and no identifier is sent anywhere.

If a key is machine-bound, the binding uses a one-way SHA-256 hash of your
Windows `MachineGuid` and computer name, truncated to 16 characters. That value
is displayed to you in *Settings → Licence* so you can quote it in a support
request. It is not reversible into your machine identity and is never
transmitted automatically.

## Children

AVAK is not directed at children and collects no data from anyone.

## Your rights

Because AVAK holds no personal data on our systems, there is nothing for us to
export, correct or erase. You have direct and complete control: the data is in a
folder you own, in readable JSON, and you can delete it at any moment.

## Changes

Material changes to this statement will be listed in `CHANGELOG.md` and shown in
the release notes.

## Contact

> Insert your support email and legal entity here before publishing.
