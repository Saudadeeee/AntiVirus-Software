# Writing a detection provider

A provider is one way of deciding whether a file is malicious. AVAK ships four
(hash, pattern, archive, heuristic); yours sits alongside them and can be
switched on and off from the Rules page like any other.

There is exactly one interface to implement and no registration code to write.

---

## 1. Create the project

```bat
dotnet new classlib -lang VB -n MyProvider
cd MyProvider
```

`MyProvider.vbproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <OptionStrict>On</OptionStrict>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <!-- Private=false: bind to the AVAK.dll the host already loaded -->
    <ProjectReference Include="..\..\AVAK.vbproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
  </ItemGroup>
</Project>
```

C# works too - the interface is plain .NET.

## 2. Implement the provider

```vb
Imports AVAK.Engine
Imports AVAK.Security

Public Class BigScriptProvider
    Inherits DetectionProviderBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return "example.bigscript"      ' unique, stable, never changes
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Oversized scripts"
        End Get
    End Property

    Public Overrides ReadOnly Property Order As Integer
        Get
            Return 400                      ' after patterns, before heuristics
        End Get
    End Property

    ' cheap filter - runs for every file, so do not read anything here
    Public Overrides Function CanHandle(context As DetectionContext) As Boolean
        Return context.HasExtension(".ps1", ".bat", ".vbs") AndAlso
               context.SizeBytes > 512 * 1024
    End Function

    Public Overrides Function Inspect(context As DetectionContext) As Detection
        Return Detect(context, "Example:Script/Oversized", Severity.Medium,
                      DetectionSource.Heuristic, 35,
                      $"A {context.Extension} script of {context.SizeBytes} bytes is unusual")
    End Function
End Class
```

## 3. Install it

```bat
dotnet build -c Release
copy bin\Release\net8.0-windows\MyProvider.dll "<AVAK folder>\plugins\"
```

Start AVAK, or press **Reload plugins** on the Rules page. Your provider is in the
list with its id, order, hit count and time spent.

`samples\SamplePlugin` is a complete working example with two providers - one
name-based, one that loads its own word list and opts out when the list is
missing.

---

## The contract

| Member | Meaning |
|---|---|
| `Id` | Unique, stable. Stored in settings when the user disables you. |
| `DisplayName` | Shown on the Rules page. |
| `Description` | One line explaining what you look for. |
| `Order` | Lower runs first. Built-ins: 100 hash, 200 pattern, 300 archive, 900 heuristic. |
| `Terminal` | `True` stops the chain on a hit. Use it for certainty (an exact match). `False` for scoring, so other evidence still gets collected. |
| `Initialise()` | Called once at startup. Load your data here. Return `False` to opt out silently - the right answer when your rule file is missing. |
| `CanHandle(ctx)` | Cheap pre-filter. Runs for every file. **Do not read the file here.** |
| `Inspect(ctx)` | The real check. Return `Nothing` for clean. |

### Rules that matter

**Never throw.** The registry counts failures and disables a provider after five,
which protects the scan but hides your bug. Catch what you can and return
`Nothing`.

**Be re-entrant.** `Inspect` runs on many threads at once. Instance fields you
write to need a lock; fields you only read after `Initialise` are fine.

**Read from the context, not the disk.** `context.Buffer` is already loaded and
shared. Opening the file again doubles the I/O and can fail on locked files.

**Be cheap in `CanHandle`.** It runs on every file in a full-drive scan. Extension
and size only.

**Pick a severity you can defend.** `Medium` and above is reported to the user and
can trigger quarantine. `Low` is recorded but not surfaced. If you are guessing,
use `Low` and let evidence accumulate.

**Explain yourself.** Every reason string you pass to `Detect` appears in the
detection details. "Imports CreateRemoteThread and VirtualAllocEx" is useful;
"Suspicious" is not.

### What DetectionContext gives you

```vb
context.FilePath            ' full path
context.FileName            ' name only
context.Extension           ' ".exe", lower case comparison via HasExtension
context.SizeBytes
context.File                ' FileInfo - attributes, timestamps, directory
context.Settings            ' the user's AppSettings
context.Trigger             ' Quick / Full / Custom / Memory / Realtime

context.Buffer              ' first 2 MB, loaded once, shared - treat as read-only
context.BufferLength        ' valid bytes in Buffer
context.Sha256              ' full-file hash, computed once
context.Md5                 ' only computed if you ask
context.IsPortableExecutable
context.HasExtension(".a", ".b")
```

---

## Testing your provider

```bat
rem does it fire where it should?
AVAK.exe --rules test C:\samples

rem does it fire where it must not? every hit here is a false positive
AVAK.exe --rules test "C:\Program Files"
```

The second command is the one that matters. A provider with a good detection rate
and a bad false-positive rate is worse than no provider: users stop trusting every
alert, including the true ones.

## Distributing

A provider DLL is just a file. Ship it with a rule pack in the same download and
tell people to drop both in place. If you want it in AVAK itself, open a pull
request - see [../CONTRIBUTING.md](../CONTRIBUTING.md).

## Security note

Plugins run in AVAK's own process with AVAK's own rights. There is no sandbox.
This is stated plainly on the Rules page: only load DLLs you built or audited.
