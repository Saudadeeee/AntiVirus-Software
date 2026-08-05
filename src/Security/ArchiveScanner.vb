Imports System.IO
Imports System.IO.Compression
Imports AVAK.Core

Namespace Security

    ''' <summary>
    ''' Inspects the entries inside ZIP-family containers (.zip, .jar, .apk and the
    ''' OOXML office formats) without writing anything to disk.
    '''
    ''' Hardened against decompression bombs: every entry is capped, the total
    ''' extracted volume is capped, the compression ratio is checked, entry names are
    ''' validated against traversal, and nesting depth is limited.
    ''' </summary>
    Public NotInheritable Class ArchiveScanner

        Private Sub New()
        End Sub

        Public Const MaxEntries As Integer = 512
        Public Const MaxEntryBytes As Long = 32L * 1024L * 1024L
        Public Const MaxTotalBytes As Long = 192L * 1024L * 1024L
        ''' <summary>uncompressed / compressed above this is treated as a bomb.</summary>
        Public Const MaxRatio As Double = 250.0R
        Public Const MaxDepth As Integer = 2

        Private Shared ReadOnly ZipExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".zip", ".jar", ".apk", ".docx", ".xlsx", ".pptx", ".docm", ".xlsm", ".pptm",
            ".odt", ".ods", ".odp", ".epub", ".nupkg", ".vsix", ".xpi", ".crx", ".whl", ".aar"}

        Public Shared Function IsSupported(extension As String) As Boolean
            Return Not String.IsNullOrEmpty(extension) AndAlso ZipExtensions.Contains(extension)
        End Function

        ''' <summary>True when the first four bytes are a local file header ("PK\3\4").</summary>
        Public Shared Function LooksLikeZip(buffer As Byte(), length As Integer) As Boolean
            Return buffer IsNot Nothing AndAlso length >= 4 AndAlso
                   buffer(0) = &H50 AndAlso buffer(1) = &H4B AndAlso
                   (buffer(2) = 3 OrElse buffer(2) = 5 OrElse buffer(2) = 7) AndAlso
                   (buffer(3) = 4 OrElse buffer(3) = 6 OrElse buffer(3) = 8)
        End Function

        Public Class ArchiveHit
            Public Property EntryName As String = ""
            Public Property ThreatName As String = ""
            Public Property Severity As Severity = Severity.Medium
            Public Property Source As DetectionSource = DetectionSource.Signature
            Public Property Reasons As New List(Of String)()
            Public Property Sha256 As String = ""
            Public Property SizeBytes As Long = 0
        End Class

        ''' <summary>
        ''' Scans one archive. Returns every entry that matched; an empty list means
        ''' the container is clean (or could not be opened, which is logged).
        ''' </summary>
        Public Shared Function Scan(archivePath As String, cfg As AppSettings,
                                    Optional depth As Integer = 0) As List(Of ArchiveHit)
            Dim hits As New List(Of ArchiveHit)()
            If depth > MaxDepth Then Return hits

            Try
                Using zip = ZipFile.OpenRead(archivePath)
                    ScanEntries(zip, cfg, depth, hits, "")
                End Using
            Catch ex As InvalidDataException
                ' not a real zip, or truncated - nothing to report
                Logger.Dbg("Archive not readable: " & archivePath)
            Catch ex As Exception
                Logger.Warn("Archive scan failed for " & archivePath & ": " & ex.Message)
            End Try

            Return hits
        End Function

        Private Shared Sub ScanEntries(zip As ZipArchive, cfg As AppSettings, depth As Integer,
                                       hits As List(Of ArchiveHit), prefix As String)
            Dim entriesSeen = 0
            Dim totalExtracted As Long = 0

            For Each entry In zip.Entries
                entriesSeen += 1
                If entriesSeen > MaxEntries Then
                    hits.Add(New ArchiveHit With {
                        .EntryName = prefix & "(" & MaxEntries & "+ entries)",
                        .ThreatName = "HEUR:Archive/TooManyEntries",
                        .Severity = Severity.Low,
                        .Source = DetectionSource.Heuristic,
                        .Reasons = New List(Of String) From {
                            $"Archive holds more than {MaxEntries} entries; AVAK stopped enumerating"}})
                    Exit For
                End If

                ' directory entries have no content
                If entry.Length = 0 AndAlso entry.Name.Length = 0 Then Continue For

                Dim fullName = prefix & entry.FullName

                ' path traversal in the entry name is malicious by construction
                If entry.FullName.Contains("..") OrElse
                   entry.FullName.StartsWith("/") OrElse entry.FullName.StartsWith("\") OrElse
                   entry.FullName.Contains(":\") Then
                    hits.Add(New ArchiveHit With {
                        .EntryName = fullName,
                        .ThreatName = "Exploit:Archive/PathTraversal",
                        .Severity = Severity.High,
                        .Source = DetectionSource.Heuristic,
                        .SizeBytes = entry.Length,
                        .Reasons = New List(Of String) From {
                            "Entry name escapes the extraction folder (Zip Slip)"}})
                    Continue For
                End If

                ' decompression bomb guards
                If entry.Length > MaxEntryBytes Then
                    hits.Add(BombHit(fullName, entry.Length, entry.CompressedLength,
                                     $"Entry expands to {Fmt.Bytes(entry.Length)}, above the {Fmt.Bytes(MaxEntryBytes)} limit"))
                    Continue For
                End If
                If entry.CompressedLength > 0 Then
                    Dim ratio = entry.Length / CDbl(entry.CompressedLength)
                    If ratio > MaxRatio AndAlso entry.Length > 1024L * 1024L Then
                        hits.Add(BombHit(fullName, entry.Length, entry.CompressedLength,
                                         $"Compression ratio {ratio:0}:1 looks like a decompression bomb"))
                        Continue For
                    End If
                End If
                If totalExtracted + entry.Length > MaxTotalBytes Then
                    hits.Add(BombHit(fullName, entry.Length, entry.CompressedLength,
                                     "Archive exceeds the total extraction budget"))
                    Exit For
                End If

                Dim data = ReadEntry(entry)
                If data Is Nothing OrElse data.Length = 0 Then Continue For
                totalExtracted += data.Length

                Dim ext = ""
                Try
                    ext = Path.GetExtension(entry.Name)
                Catch
                End Try

                ' 1. exact hash
                Dim sha = SignatureDatabase.Sha256Bytes(data, data.Length)
                Dim known = SignatureDatabase.MatchHash(sha, "")
                If known IsNot Nothing Then
                    hits.Add(New ArchiveHit With {
                        .EntryName = fullName, .ThreatName = known.Name, .Severity = known.Severity,
                        .Source = DetectionSource.Signature, .Sha256 = sha, .SizeBytes = data.Length,
                        .Reasons = New List(Of String) From {"Exact signature match inside the archive"}})
                    Continue For
                End If

                ' 2. byte patterns
                Dim pats = SignatureDatabase.MatchPatterns(data, data.Length, ext)
                If pats.Count > 0 Then
                    Dim worst = pats.OrderByDescending(Function(p) p.Severity).First()
                    hits.Add(New ArchiveHit With {
                        .EntryName = fullName, .ThreatName = worst.Name, .Severity = worst.Severity,
                        .Source = DetectionSource.Pattern, .Sha256 = sha, .SizeBytes = data.Length,
                        .Reasons = New List(Of String) From {
                            "Byte pattern inside the archive: " & String.Join(", ", pats.Select(Function(p) p.Name).Take(3))}})
                    Continue For
                End If

                ' 3. heuristics
                If cfg.HeuristicsEnabled Then
                    Dim h = HeuristicAnalyzer.Analyze(entry.Name, data, data.Length, Nothing, cfg.HeuristicSensitivity)
                    If h.Severity >= Severity.Medium Then
                        hits.Add(New ArchiveHit With {
                            .EntryName = fullName,
                            .ThreatName = If(h.IsPortableExecutable, "HEUR:Archive/Win32.Gen", "HEUR:Archive/Script.Gen"),
                            .Severity = h.Severity, .Source = DetectionSource.Heuristic,
                            .Sha256 = sha, .SizeBytes = data.Length, .Reasons = h.Reasons})
                        Continue For
                    End If
                End If

                ' 4. nested archive
                If depth < MaxDepth AndAlso LooksLikeZip(data, data.Length) Then
                    Try
                        Using ms As New MemoryStream(data, False)
                            Using inner As New ZipArchive(ms, ZipArchiveMode.Read)
                                ScanEntries(inner, cfg, depth + 1, hits, fullName & " > ")
                            End Using
                        End Using
                    Catch
                    End Try
                End If
            Next
        End Sub

        Private Shared Function BombHit(name As String, expanded As Long, packed As Long, why As String) As ArchiveHit
            Return New ArchiveHit With {
                .EntryName = name,
                .ThreatName = "HEUR:Archive/DecompressionBomb",
                .Severity = Severity.Medium,
                .Source = DetectionSource.Heuristic,
                .SizeBytes = expanded,
                .Reasons = New List(Of String) From {
                    why, $"{Fmt.Bytes(packed)} packed -> {Fmt.Bytes(expanded)} unpacked"}}
        End Function

        ''' <summary>Reads at most <see cref="MaxEntryBytes"/>, refusing to trust entry.Length.</summary>
        Private Shared Function ReadEntry(entry As ZipArchiveEntry) As Byte()
            Try
                Dim cap = CInt(Math.Min(MaxEntryBytes, 4L * 1024L * 1024L))   ' 4 MB window is plenty for detection
                Using stream = entry.Open()
                    Using ms As New MemoryStream()
                        Dim buf(65535) As Byte
                        Dim total = 0
                        While total < cap
                            Dim n = stream.Read(buf, 0, Math.Min(buf.Length, cap - total))
                            If n <= 0 Then Exit While
                            ms.Write(buf, 0, n)
                            total += n
                        End While
                        Return ms.ToArray()
                    End Using
                End Using
            Catch
                Return Nothing
            End Try
        End Function

    End Class

End Namespace
