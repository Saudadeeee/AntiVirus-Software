Imports System.IO
Imports System.Text
Imports AVAK.Core
Imports AVAK.Security

Namespace Content

    ''' <summary>
    ''' Command-line rule authoring, so the framework can be driven from a script
    ''' or a CI job without opening the UI.
    '''
    '''   AVAK.exe --rules list
    '''   AVAK.exe --rules new --pack "My rules"
    '''   AVAK.exe --rules add-hash --file bad.exe --name "Trojan.Test" [--severity High] [--pack my-rules.rules.json]
    '''   AVAK.exe --rules add-hash --sha256 &lt;hex&gt; --name "..."
    '''   AVAK.exe --rules add-pattern --ascii "text" --name "..." [--ext .bat,.cmd] [--max-offset 4096]
    '''   AVAK.exe --rules add-pattern --hex "4D5A??00" --name "..."
    '''   AVAK.exe --rules remove --name "..." --pack my-rules.rules.json
    '''   AVAK.exe --rules validate
    '''   AVAK.exe --rules test &lt;folder&gt;
    '''   AVAK.exe --rules import &lt;file.json&gt;
    '''
    ''' Output is written to rules-cli.txt next to the executable (AVAK is a windowed
    ''' app and has no console of its own) and mirrored to the log.
    ''' </summary>
    Public NotInheritable Class RuleCli

        Private Sub New()
        End Sub

        Public Shared Function Run(args As String()) As Integer
            Dim outp As New StringBuilder()
            Dim code = 0

            Try
                Dim verb = Positional(args, "--rules").ToLowerInvariant()

                Select Case verb
                    Case "list" : code = CmdList(outp)
                    Case "new" : code = CmdNew(args, outp)
                    Case "add-hash" : code = CmdAddHash(args, outp)
                    Case "add-pattern" : code = CmdAddPattern(args, outp)
                    Case "remove" : code = CmdRemove(args, outp)
                    Case "validate" : code = CmdValidate(outp)
                    Case "test" : code = CmdTest(args, outp)
                    Case "import" : code = CmdImport(args, outp)
                    Case Else
                        Usage(outp)
                        code = 2
                End Select
            Catch ex As Exception
                outp.AppendLine("Failed: " & ex.Message)
                code = 1
            End Try

            Dim text = outp.ToString()
            Try
                File.WriteAllText(Path.Combine(AppPaths.AppDir, "rules-cli.txt"), text, New UTF8Encoding(False))
            Catch
            End Try
            Logger.Info("rules cli:" & Environment.NewLine & text)
            Return code
        End Function

        Private Shared Sub Usage(o As StringBuilder)
            o.AppendLine("AVAK rule tool")
            o.AppendLine()
            o.AppendLine("  --rules list")
            o.AppendLine("  --rules new --pack ""Name""")
            o.AppendLine("  --rules add-hash (--file <path> | --sha256 <hex>) --name <threat> [--severity Low|Medium|High|Critical] [--pack <file>]")
            o.AppendLine("  --rules add-pattern (--ascii <text> | --hex <bytes>) --name <threat> [--severity ...] [--ext .a,.b] [--max-offset N] [--pack <file>]")
            o.AppendLine("  --rules remove --name <threat> --pack <file>")
            o.AppendLine("  --rules validate")
            o.AppendLine("  --rules test <folder>")
            o.AppendLine("  --rules import <file.json>")
        End Sub

        ' -- commands ----------------------------------------------------------

        Private Shared Function CmdList(o As StringBuilder) As Integer
            RuleStore.Reload()
            Dim packs = RuleStore.Packs()
            o.AppendLine($"{packs.Count} rule pack(s)")
            o.AppendLine()
            For Each p In packs
                o.AppendLine($"[{If(p.Enabled, "on ", "off")}] {p.Name}  (v{p.Version})")
                o.AppendLine($"        file    : {p.FilePath}{If(p.ReadOnlyPack, "   [shipped, read-only]", "")}")
                o.AppendLine($"        rules   : {p.Hashes.Count} hash, {p.Patterns.Count} pattern")
                If Not String.IsNullOrWhiteSpace(p.Author) Then o.AppendLine($"        author  : {p.Author}")
                For Each issue In p.Issues.Take(5)
                    o.AppendLine($"        WARNING : {issue}")
                Next
                o.AppendLine()
            Next
            o.AppendLine($"Active: {RuleStore.HashCount} hash signatures, {RuleStore.PatternCount} patterns")
            Return 0
        End Function

        Private Shared Function CmdNew(args As String(), o As StringBuilder) As Integer
            Dim name = Value(args, "--pack")
            If String.IsNullOrWhiteSpace(name) Then
                o.AppendLine("--pack <name> is required.")
                Return 2
            End If
            Dim path = RuleStore.CreatePack(name, Environment.UserName)
            o.AppendLine("Created " & path)
            Return 0
        End Function

        Private Shared Function CmdAddHash(args As String(), o As StringBuilder) As Integer
            Dim sha = Value(args, "--sha256")
            Dim md5 = Value(args, "--md5")
            Dim file = Value(args, "--file")

            If Not String.IsNullOrWhiteSpace(file) Then
                If Not IO.File.Exists(file) Then
                    o.AppendLine("File not found: " & file)
                    Return 2
                End If
                sha = SignatureDatabase.Sha256File(file)
                md5 = SignatureDatabase.Md5File(file)
                o.AppendLine("Hashed " & file)
                o.AppendLine("  sha256 " & sha)
                o.AppendLine("  md5    " & md5)
            End If

            Dim name = Value(args, "--name")
            If String.IsNullOrWhiteSpace(name) Then name = "Custom.Hash"

            Dim result = RuleStore.AddHash(sha, name, ParseSeverity(Value(args, "--severity")),
                                           md5, ResolvePack(args))
            o.AppendLine(result.Message)
            Return If(result.Ok, 0, 1)
        End Function

        Private Shared Function CmdAddPattern(args As String(), o As StringBuilder) As Integer
            Dim p As New PatternSignature With {
                .Name = Value(args, "--name"),
                .Ascii = Value(args, "--ascii"),
                .Hex = Value(args, "--hex"),
                .Severity = ParseSeverity(Value(args, "--severity"))
            }
            If String.IsNullOrWhiteSpace(p.Name) Then p.Name = "Custom.Pattern"

            Dim maxOffset = 0
            Integer.TryParse(Value(args, "--max-offset"), maxOffset)
            p.MaxOffset = maxOffset

            Dim exts = Value(args, "--ext")
            If Not String.IsNullOrWhiteSpace(exts) Then
                p.Extensions = exts.Split(","c).
                    Select(Function(e) e.Trim()).
                    Where(Function(e) e.Length > 0).
                    Select(Function(e) If(e.StartsWith("."), e, "." & e)).ToList()
            End If

            Dim result = RuleStore.AddPattern(p, ResolvePack(args))
            o.AppendLine(result.Message)
            Return If(result.Ok, 0, 1)
        End Function

        Private Shared Function CmdRemove(args As String(), o As StringBuilder) As Integer
            Dim name = Value(args, "--name")
            Dim pack = ResolvePack(args)
            If String.IsNullOrWhiteSpace(name) OrElse String.IsNullOrWhiteSpace(pack) Then
                o.AppendLine("--name and --pack are both required.")
                Return 2
            End If
            Dim result = RuleStore.RemoveRule(pack, name)
            o.AppendLine(result.Message)
            Return If(result.Ok, 0, 1)
        End Function

        Private Shared Function CmdValidate(o As StringBuilder) As Integer
            RuleStore.Reload()
            Dim problems = 0
            For Each p In RuleStore.Packs()
                Dim issues = p.Validate()
                If issues.Count = 0 Then
                    o.AppendLine($"OK    {p.FileName}  ({p.RuleCount} rules)")
                Else
                    problems += issues.Count
                    o.AppendLine($"FAIL  {p.FileName}")
                    For Each i In issues
                        o.AppendLine("        " & i)
                    Next
                End If
            Next
            o.AppendLine()
            o.AppendLine(If(problems = 0, "All packs are valid.", $"{problems} problem(s) found."))
            Return If(problems = 0, 0, 1)
        End Function

        ''' <summary>
        ''' Runs the whole engine over a folder and prints what fired. Point it at a
        ''' clean corpus and every hit is a false positive.
        ''' </summary>
        Private Shared Function CmdTest(args As String(), o As StringBuilder) As Integer
            Dim folder = Positional(args, "test")
            If String.IsNullOrWhiteSpace(folder) Then folder = LastPositional(args)
            If String.IsNullOrWhiteSpace(folder) OrElse Not Directory.Exists(folder) Then
                o.AppendLine("Usage: --rules test <folder>")
                Return 2
            End If

            RuleStore.Reload()
            Dim cfg = AppSettings.Current
            Dim files As String()
            Try
                files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            Catch ex As Exception
                o.AppendLine("Cannot enumerate: " & ex.Message)
                Return 1
            End Try

            Dim hits = 0
            Dim sw = Stopwatch.StartNew()
            Dim bySeverity As New Dictionary(Of Severity, Integer)()

            o.AppendLine($"Testing {files.Length} file(s) under {folder}")
            o.AppendLine()

            For Each f In files
                Try
                    Dim fi As New FileInfo(f)
                    If Not fi.Exists OrElse fi.Length = 0 Then Continue For
                    Dim det = ScanEngine.InspectFile(f, fi, cfg)
                    If det Is Nothing Then Continue For

                    hits += 1
                    bySeverity(det.Severity) = If(bySeverity.ContainsKey(det.Severity), bySeverity(det.Severity), 0) + 1
                    o.AppendLine($"{det.Severity,-8} {det.ThreatName}")
                    o.AppendLine($"         {f}")
                    For Each r In det.Reasons.Take(3)
                        o.AppendLine($"           - {r}")
                    Next
                Catch ex As Exception
                    o.AppendLine($"ERROR    {f}: {ex.Message}")
                End Try
            Next
            sw.Stop()

            o.AppendLine()
            o.AppendLine($"{hits} detection(s) in {files.Length} file(s), {sw.ElapsedMilliseconds} ms")
            For Each kv In bySeverity.OrderByDescending(Function(k) k.Key)
                o.AppendLine($"  {kv.Key,-8} {kv.Value}")
            Next
            o.AppendLine()
            o.AppendLine("If this folder is known-clean, every line above is a false positive.")
            Return hits
        End Function

        Private Shared Function CmdImport(args As String(), o As StringBuilder) As Integer
            Dim file = LastPositional(args)
            If String.IsNullOrWhiteSpace(file) OrElse Not IO.File.Exists(file) Then
                o.AppendLine("Usage: --rules import <file.json>")
                Return 2
            End If
            Dim result = RuleStore.ImportPack(file)
            o.AppendLine(result.Message)
            Return If(result.Ok, 0, 1)
        End Function

        ' -- argument helpers --------------------------------------------------

        Private Shared Function Value(args As String(), flag As String) As String
            Dim i = Array.FindIndex(args, Function(a) String.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
            If i < 0 OrElse i + 1 >= args.Length Then Return ""
            Dim v = args(i + 1)
            Return If(v.StartsWith("--"), "", v)
        End Function

        ''' <summary>The token immediately after a flag, even when it looks like a verb.</summary>
        Private Shared Function Positional(args As String(), flag As String) As String
            Dim i = Array.FindIndex(args, Function(a) String.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
            If i < 0 OrElse i + 1 >= args.Length Then Return ""
            Return args(i + 1)
        End Function

        Private Shared Function LastPositional(args As String()) As String
            For i = args.Length - 1 To 0 Step -1
                If Not args(i).StartsWith("--") Then
                    If i = 0 OrElse Not args(i - 1).StartsWith("--") OrElse
                       String.Equals(args(i - 1), "--rules", StringComparison.OrdinalIgnoreCase) Then
                        If Not String.Equals(args(i), "test", StringComparison.OrdinalIgnoreCase) AndAlso
                           Not String.Equals(args(i), "import", StringComparison.OrdinalIgnoreCase) Then
                            Return args(i)
                        End If
                    End If
                    Return args(i)
                End If
            Next
            Return ""
        End Function

        ''' <summary>Turns --pack into a full path; a bare name is resolved in the user folder.</summary>
        Private Shared Function ResolvePack(args As String()) As String
            Dim pack = Value(args, "--pack")
            If String.IsNullOrWhiteSpace(pack) Then Return Nothing
            If IO.File.Exists(pack) Then Return pack

            Dim candidate = Path.Combine(RuleStore.UserFolder, pack)
            If IO.File.Exists(candidate) Then Return candidate
            candidate = Path.Combine(RuleStore.UserFolder, pack & RuleStore.PackExtension)
            If IO.File.Exists(candidate) Then Return candidate

            Return RuleStore.CreatePack(pack, Environment.UserName)
        End Function

        Private Shared Function ParseSeverity(s As String) As Severity
            Dim sev As Severity
            If Not String.IsNullOrWhiteSpace(s) AndAlso [Enum].TryParse(s, True, sev) Then Return sev
            Return Severity.High
        End Function

    End Class

End Namespace
