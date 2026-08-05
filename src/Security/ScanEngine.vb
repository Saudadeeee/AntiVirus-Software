Imports System.Collections.Concurrent
Imports System.IO
Imports System.Threading
Imports AVAK.Core

Namespace Security

    Public Class ScanRequest
        Public Property Profile As ScanProfile = ScanProfile.Quick
        Public Property Roots As New List(Of String)()
        Public Property Recursive As Boolean = True
        Public Property MaxFiles As Integer = 0          ' 0 = unlimited
    End Class

    ''' <summary>
    ''' The scanner. Enumerates targets, then hashes + pattern-matches + heuristically
    ''' inspects each file in parallel, reporting progress and honouring cancellation.
    ''' </summary>
    Public Class ScanEngine

        Public Event ThreatFound As EventHandler(Of Detection)

        Private Shared ReadOnly SkipDirNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "$Recycle.Bin", "System Volume Information", "WinSxS", "DriverStore",
            "Windows Defender", "servicing", "Installer", "SoftwareDistribution",
            "node_modules", ".git", ".svn", "obj", "bin"}

        Private Shared ReadOnly SkipRootPaths As New List(Of String) From {
            "\Windows\WinSxS", "\Windows\servicing", "\Windows\assembly",
            "\Windows\System32\DriverStore", "\Windows\SoftwareDistribution"}

        ''' <summary>Extensions we always read; anything else still gets hashed but not deep-parsed.</summary>
        Private Shared ReadOnly InterestingExts As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".exe", ".dll", ".scr", ".com", ".sys", ".ocx", ".cpl", ".msi", ".jar", ".apk",
            ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".bat", ".cmd", ".reg", ".lnk",
            ".doc", ".docm", ".xls", ".xlsm", ".ppt", ".pptm", ".docx", ".xlsx", ".pptx", ".rtf", ".pdf",
            ".zip", ".rar", ".7z", ".iso", ".img", ".inf", ".py", ".sh", ".jsp", ".php"}

        Public Async Function ScanAsync(req As ScanRequest,
                                        progress As IProgress(Of ScanProgressInfo),
                                        ct As CancellationToken) As Task(Of ScanReport)
            Dim cfg = AppSettings.Current
            SignatureDatabase.EnsureLoaded()

            Dim report As New ScanReport With {
                .Profile = req.Profile,
                .StartedAt = DateTime.Now,
                .Roots = req.Roots.ToList()
            }
            Dim sw = Stopwatch.StartNew()

            Return Await Task.Run(
                Function()
                    Try
                        ' -- phase 1: enumerate ---------------------------------
                        progress?.Report(New ScanProgressInfo With {.Phase = "Building the file list...", .Elapsed = sw.Elapsed})

                        Dim targets As List(Of String)
                        If req.Profile = ScanProfile.Memory Then
                            targets = RunningImages()
                        Else
                            targets = Enumerate(req, cfg, ct, progress, sw)
                        End If

                        If ct.IsCancellationRequested Then
                            report.Cancelled = True
                            report.DurationMs = sw.ElapsedMilliseconds
                            Return report
                        End If

                        Dim total As Long = targets.Count
                        progress?.Report(New ScanProgressInfo With {
                            .Phase = $"Scanning {Fmt.Num(total)} files...",
                            .TotalFiles = total, .Elapsed = sw.Elapsed})

                        ' -- phase 2: inspect -----------------------------------
                        Dim scanned As Long = 0, bytes As Long = 0, skipped As Long = 0, errors As Long = 0
                        Dim found As New ConcurrentBag(Of Detection)()
                        Dim maxBytes As Long = CLng(cfg.MaxFileSizeMb) * 1024L * 1024L
                        Dim lastReport As Long = 0

                        Dim po As New ParallelOptions With {
                            .MaxDegreeOfParallelism = cfg.EffectiveThreads(),
                            .CancellationToken = ct
                        }

                        Try
                            Parallel.ForEach(targets, po,
                                Sub(path)
                                    po.CancellationToken.ThrowIfCancellationRequested()
                                    Try
                                        Dim fi As New FileInfo(path)
                                        If Not fi.Exists Then
                                            Interlocked.Increment(skipped)
                                            Return
                                        End If
                                        If fi.Length = 0 OrElse fi.Length > maxBytes Then
                                            Interlocked.Increment(skipped)
                                            Return
                                        End If

                                        Dim det = InspectFile(path, fi, cfg)
                                        Interlocked.Add(bytes, fi.Length)

                                        If det IsNot Nothing Then
                                            found.Add(det)
                                            RaiseEvent ThreatFound(Me, det)
                                        End If
                                    Catch ex As OperationCanceledException
                                        Throw
                                    Catch
                                        Interlocked.Increment(errors)
                                    Finally
                                        Dim n = Interlocked.Increment(scanned)
                                        ' throttle UI updates: at most every 24 files
                                        If progress IsNot Nothing AndAlso (n - Interlocked.Read(lastReport)) >= 24 Then
                                            Interlocked.Exchange(lastReport, n)
                                            progress.Report(New ScanProgressInfo With {
                                                .FilesScanned = n,
                                                .TotalFiles = total,
                                                .BytesScanned = Interlocked.Read(bytes),
                                                .CurrentFile = path,
                                                .ThreatsFound = found.Count,
                                                .Elapsed = sw.Elapsed,
                                                .Phase = "Scanning"})
                                        End If
                                    End Try
                                End Sub)
                        Catch ex As OperationCanceledException
                            report.Cancelled = True
                        End Try

                        report.FilesScanned = scanned
                        report.BytesScanned = bytes
                        report.SkippedFiles = skipped
                        report.ErrorCount = errors
                        report.Detections = found.OrderByDescending(Function(d) d.Severity).
                                                  ThenBy(Function(d) d.FilePath).ToList()
                        report.DurationMs = sw.ElapsedMilliseconds

                        progress?.Report(New ScanProgressInfo With {
                            .FilesScanned = scanned, .TotalFiles = total, .BytesScanned = bytes,
                            .ThreatsFound = report.ThreatCount, .Elapsed = sw.Elapsed, .Phase = "Done"})

                        Return report
                    Catch ex As Exception
                        Logger.Error("Scan crashed", ex)
                        report.DurationMs = sw.ElapsedMilliseconds
                        Return report
                    End Try
                End Function, ct)
        End Function

        ''' <summary>
        ''' Inspects a single file by running every enabled detection provider.
        ''' Returns Nothing when clean. Also used by the realtime monitor and the CLI.
        '''
        ''' Add a provider instead of editing this method - see docs/EXTENDING.md.
        ''' </summary>
        Public Shared Function InspectFile(filePath As String, fi As FileInfo, cfg As AppSettings,
                                           Optional trigger As ScanProfile = ScanProfile.Custom) As Detection
            Dim ext = ""
            Try
                ext = Path.GetExtension(filePath)
            Catch
            End Try

            If cfg.ExcludedExtensions IsNot Nothing AndAlso
               cfg.ExcludedExtensions.Any(Function(e) String.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) Then
                Return Nothing
            End If
            If IsExcluded(filePath, cfg) Then Return Nothing

            Dim context As New Engine.DetectionContext(filePath, fi, cfg, trigger)
            Return Engine.ProviderRegistry.Inspect(context)
        End Function


        ''' <summary>
        ''' Exclusion match. A plain entry is a prefix match (folder or exact file);
        ''' an entry containing * or ? is treated as a glob against both the full path
        ''' and the file name, so "*.iso" and "C:\Builds\*\bin" both work.
        ''' </summary>
        Public Shared Function IsExcluded(path As String, cfg As AppSettings) As Boolean
            If cfg.ExcludedPaths Is Nothing OrElse cfg.ExcludedPaths.Count = 0 Then Return False
            Dim name As String = Nothing

            For Each ex In cfg.ExcludedPaths
                If String.IsNullOrWhiteSpace(ex) Then Continue For
                Dim pattern = ex.Trim()

                If pattern.IndexOf("*"c) < 0 AndAlso pattern.IndexOf("?"c) < 0 Then
                    If path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) Then Return True
                    Continue For
                End If

                If name Is Nothing Then
                    Try
                        name = IO.Path.GetFileName(path)
                    Catch
                        name = path
                    End Try
                End If

                If GlobMatch(path, pattern) OrElse GlobMatch(name, pattern) Then Return True
            Next
            Return False
        End Function

        ''' <summary>Case-insensitive wildcard match supporting * and ?, without regex.</summary>
        Public Shared Function GlobMatch(text As String, pattern As String) As Boolean
            If text Is Nothing OrElse pattern Is Nothing Then Return False
            Dim t = text.ToLowerInvariant()
            Dim p = pattern.ToLowerInvariant()

            Dim ti = 0, pi = 0
            Dim starP = -1, starT = 0

            While ti < t.Length
                If pi < p.Length AndAlso (p(pi) = "?"c OrElse p(pi) = t(ti)) Then
                    ti += 1
                    pi += 1
                ElseIf pi < p.Length AndAlso p(pi) = "*"c Then
                    starP = pi
                    starT = ti
                    pi += 1
                ElseIf starP >= 0 Then
                    pi = starP + 1
                    starT += 1
                    ti = starT
                Else
                    Return False
                End If
            End While

            While pi < p.Length AndAlso p(pi) = "*"c
                pi += 1
            End While
            Return pi = p.Length
        End Function

        ' -- file window ----------------------------------------------------------

        Public Structure Window
            Public Data As Byte()
            Public Length As Integer
        End Structure

        ''' <summary>Reads up to 2 MB from the head of the file - enough for headers, imports and scripts.</summary>
        Public Shared Function ReadWindow(path As String, size As Long) As Window
            Const Cap As Integer = 2 * 1024 * 1024
            Try
                Dim want = CInt(Math.Min(size, Cap))
                Dim buf(want - 1) As Byte
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536)
                    Dim read = 0
                    While read < want
                        Dim n = fs.Read(buf, read, want - read)
                        If n <= 0 Then Exit While
                        read += n
                    End While
                    Return New Window With {.Data = buf, .Length = read}
                End Using
            Catch
                Return New Window With {.Data = Nothing, .Length = 0}
            End Try
        End Function

        ' -- enumeration ----------------------------------------------------------

        Private Shared Function Enumerate(req As ScanRequest, cfg As AppSettings, ct As CancellationToken,
                                          progress As IProgress(Of ScanProgressInfo), sw As Stopwatch) As List(Of String)
            Dim roots = If(req.Roots IsNot Nothing AndAlso req.Roots.Count > 0, req.Roots, DefaultRoots(req.Profile))
            Dim result As New List(Of String)()
            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each root In roots
                If ct.IsCancellationRequested Then Exit For
                Try
                    If File.Exists(root) Then
                        If seen.Add(root) Then result.Add(root)
                        Continue For
                    End If
                    If Not Directory.Exists(root) Then Continue For
                Catch
                    Continue For
                End Try

                Walk(root, req.Recursive, cfg, ct, result, seen, req.Profile)

                If progress IsNot Nothing Then
                    progress.Report(New ScanProgressInfo With {
                        .Phase = $"Indexing... {Fmt.Num(result.Count)} files",
                        .TotalFiles = result.Count, .Elapsed = sw.Elapsed})
                End If

                If req.MaxFiles > 0 AndAlso result.Count >= req.MaxFiles Then Exit For
            Next

            If req.MaxFiles > 0 AndAlso result.Count > req.MaxFiles Then
                result = result.Take(req.MaxFiles).ToList()
            End If
            Return result
        End Function

        Private Shared Sub Walk(dir As String, recurse As Boolean, cfg As AppSettings, ct As CancellationToken,
                                sink As List(Of String), seen As HashSet(Of String), profile As ScanProfile)
            If ct.IsCancellationRequested Then Return
            If IsExcluded(dir, cfg) Then Return

            Dim lower = dir.ToLowerInvariant()
            For Each skip In SkipRootPaths
                If lower.EndsWith(skip.ToLowerInvariant()) OrElse lower.Contains(skip.ToLowerInvariant()) Then Return
            Next

            Try
                For Each f In Directory.EnumerateFiles(dir)
                    If ct.IsCancellationRequested Then Return
                    If profile = ScanProfile.Quick Then
                        Dim ext = Path.GetExtension(f)
                        If Not InterestingExts.Contains(ext) Then Continue For
                    End If
                    If seen.Add(f) Then sink.Add(f)
                Next
            Catch
                ' unreadable directory - move on
            End Try

            If Not recurse Then Return

            Try
                For Each sub_ In Directory.EnumerateDirectories(dir)
                    If ct.IsCancellationRequested Then Return
                    Dim name = Path.GetFileName(sub_)
                    If SkipDirNames.Contains(name) Then Continue For
                    Try
                        Dim di As New DirectoryInfo(sub_)
                        If (di.Attributes And FileAttributes.ReparsePoint) = FileAttributes.ReparsePoint Then Continue For
                    Catch
                        Continue For
                    End Try
                    Walk(sub_, True, cfg, ct, sink, seen, profile)
                Next
            Catch
            End Try
        End Sub

        Public Shared Function DefaultRoots(profile As ScanProfile) As List(Of String)
            Dim l As New List(Of String)()
            Select Case profile
                Case ScanProfile.Quick
                    AddIfExists(l, Path.GetTempPath())
                    AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
                    AddIfExists(l, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
                    AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.Startup))
                    AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup))
                    AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
                    AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))

                Case ScanProfile.Full
                    Try
                        For Each d In DriveInfo.GetDrives()
                            If d.IsReady AndAlso d.DriveType = DriveType.Fixed Then l.Add(d.RootDirectory.FullName)
                        Next
                    Catch
                    End Try
                    If l.Count = 0 Then AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))

                Case Else
                    AddIfExists(l, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            End Select
            Return l.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        End Function

        Private Shared Sub AddIfExists(l As List(Of String), p As String)
            Try
                If Not String.IsNullOrWhiteSpace(p) AndAlso Directory.Exists(p) Then l.Add(p)
            Catch
            End Try
        End Sub

        ''' <summary>Distinct on-disk images of every running process (memory scan target list).</summary>
        Public Shared Function RunningImages() As List(Of String)
            Dim l As New List(Of String)()
            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each p In Process.GetProcesses()
                Try
                    Dim f = p.MainModule?.FileName
                    If Not String.IsNullOrEmpty(f) AndAlso File.Exists(f) AndAlso seen.Add(f) Then l.Add(f)
                Catch
                    ' access denied on protected processes is normal without elevation
                Finally
                    p.Dispose()
                End Try
            Next
            Return l
        End Function

    End Class

End Namespace
