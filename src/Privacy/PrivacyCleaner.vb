Imports System.IO
Imports Microsoft.Win32
Imports AVAK.Core

Namespace Privacy

    Public Class CleanTarget
        Public Property Key As String = ""
        Public Property Title As String = ""
        Public Property Description As String = ""
        Public Property IconName As String = "trash"
        Public Property Paths As New List(Of String)()
        Public Property FilePatterns As New List(Of String)()
        Public Property Recursive As Boolean = True
        Public Property Selected As Boolean = True
        Public Property Risky As Boolean = False

        Public Property SizeBytes As Long = 0
        Public Property FileCount As Integer = 0
        Public Property Measured As Boolean = False
    End Class

    Public Class CleanResult
        Public Property FreedBytes As Long = 0
        Public Property DeletedFiles As Integer = 0
        Public Property FailedFiles As Integer = 0
        Public Property Messages As New List(Of String)()
    End Class

    ''' <summary>
    ''' Deletes real junk: temp files, browser caches, thumbnail caches, crash dumps,
    ''' recent-document shortcuts and the recycle bin. Every target is measured before
    ''' anything is removed and nothing outside the listed folders is ever touched.
    ''' </summary>
    Public NotInheritable Class PrivacyCleaner

        Private Sub New()
        End Sub

        Private Shared Function Local() As String
            Return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        End Function

        Private Shared Function Roaming() As String
            Return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        End Function

        Public Shared Function Targets() As List(Of CleanTarget)
            Dim l As New List(Of CleanTarget)()

            l.Add(New CleanTarget With {
                .Key = "usertemp", .Title = "Temporary files (user)", .IconName = "trash",
                .Description = "%TEMP% - installers, extraction leftovers, crash logs",
                .Paths = {Path.GetTempPath()}.ToList()})

            l.Add(New CleanTarget With {
                .Key = "wintemp", .Title = "Temporary files (Windows)", .IconName = "trash",
                .Description = "C:\Windows\Temp - may need administrator rights",
                .Paths = {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")}.ToList()})

            l.Add(New CleanTarget With {
                .Key = "thumbs", .Title = "Thumbnail cache", .IconName = "eye",
                .Description = "Explorer thumbnail database - rebuilt automatically",
                .Paths = {Path.Combine(Local(), "Microsoft\Windows\Explorer")}.ToList(),
                .FilePatterns = {"thumbcache_*.db", "iconcache_*.db"}.ToList(),
                .Recursive = False})

            l.Add(New CleanTarget With {
                .Key = "recent", .Title = "Recent documents", .IconName = "clock",
                .Description = "Shortcuts to files you opened recently",
                .Paths = {Environment.GetFolderPath(Environment.SpecialFolder.Recent)}.ToList()})

            l.Add(New CleanTarget With {
                .Key = "crashdumps", .Title = "Crash dumps", .IconName = "file-alert",
                .Description = "Windows Error Reporting archives and queued reports",
                .Paths = {Path.Combine(Local(), "CrashDumps"),
                          Path.Combine(Local(), "Microsoft\Windows\WER\ReportArchive"),
                          Path.Combine(Local(), "Microsoft\Windows\WER\ReportQueue")}.ToList()})

            l.Add(New CleanTarget With {
                .Key = "chrome", .Title = "Chrome / Edge cache", .IconName = "globe",
                .Description = "Browser cache only - history, passwords and cookies are left alone",
                .Paths = {Path.Combine(Local(), "Google\Chrome\User Data\Default\Cache"),
                          Path.Combine(Local(), "Google\Chrome\User Data\Default\Code Cache"),
                          Path.Combine(Local(), "Microsoft\Edge\User Data\Default\Cache"),
                          Path.Combine(Local(), "Microsoft\Edge\User Data\Default\Code Cache"),
                          Path.Combine(Local(), "BraveSoftware\Brave-Browser\User Data\Default\Cache")}.ToList()})

            l.Add(New CleanTarget With {
                .Key = "firefox", .Title = "Firefox cache", .IconName = "globe",
                .Description = "cache2 entries for every Firefox profile",
                .Paths = FirefoxCaches()})

            l.Add(New CleanTarget With {
                .Key = "dxcache", .Title = "Shader / DirectX cache", .IconName = "zap",
                .Description = "Recompiled automatically by games and GPU drivers",
                .Paths = {Path.Combine(Local(), "D3DSCache"),
                          Path.Combine(Local(), "NVIDIA\DXCache"),
                          Path.Combine(Local(), "AMD\DxCache")}.ToList()})

            l.Add(New CleanTarget With {
                .Key = "delivery", .Title = "Windows Update leftovers", .IconName = "download",
                .Description = "Delivery Optimization cache - administrator rights required",
                .Paths = {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                                       "SoftwareDistribution\Download")}.ToList(),
                .Selected = False, .Risky = True})

            l.Add(New CleanTarget With {
                .Key = "recycle", .Title = "Recycle Bin", .IconName = "trash",
                .Description = "Empties the Recycle Bin on every drive",
                .Selected = False, .Risky = True})

            Return l
        End Function

        Private Shared Function FirefoxCaches() As List(Of String)
            Dim l As New List(Of String)()
            Try
                Dim root = Path.Combine(Local(), "Mozilla\Firefox\Profiles")
                If Directory.Exists(root) Then
                    For Each p In Directory.EnumerateDirectories(root)
                        Dim c = Path.Combine(p, "cache2")
                        If Directory.Exists(c) Then l.Add(c)
                    Next
                End If
            Catch
            End Try
            Return l
        End Function

        ''' <summary>Measures a target without deleting anything.</summary>
        Public Shared Sub Measure(t As CleanTarget)
            t.SizeBytes = 0
            t.FileCount = 0
            Try
                If t.Key = "recycle" Then
                    MeasureRecycleBin(t)
                Else
                    For Each p In t.Paths
                        MeasureFolder(p, t)
                    Next
                End If
            Catch ex As Exception
                Logger.Warn("Measure failed for " & t.Key & ": " & ex.Message)
            End Try
            t.Measured = True
        End Sub

        Private Shared Sub MeasureFolder(folder As String, t As CleanTarget)
            Try
                If String.IsNullOrEmpty(folder) OrElse Not Directory.Exists(folder) Then Return
                Dim opts = If(t.Recursive, SearchOption.AllDirectories, SearchOption.TopDirectoryOnly)
                Dim patterns = If(t.FilePatterns.Count > 0, t.FilePatterns, New List(Of String) From {"*"})
                For Each pat In patterns
                    For Each f In SafeFiles(folder, pat, opts)
                        Try
                            Dim fi As New FileInfo(f)
                            t.SizeBytes += fi.Length
                            t.FileCount += 1
                        Catch
                        End Try
                    Next
                Next
            Catch
            End Try
        End Sub

        Private Shared Iterator Function SafeFiles(folder As String, pattern As String,
                                                   opt As SearchOption) As IEnumerable(Of String)
            Dim stack As New Stack(Of String)()
            stack.Push(folder)
            While stack.Count > 0
                Dim dir = stack.Pop()
                Dim files As String() = Nothing
                Try
                    files = Directory.GetFiles(dir, pattern)
                Catch
                End Try
                If files IsNot Nothing Then
                    For Each f In files
                        Yield f
                    Next
                End If
                If opt = SearchOption.AllDirectories Then
                    Try
                        For Each d In Directory.GetDirectories(dir)
                            stack.Push(d)
                        Next
                    Catch
                    End Try
                End If
            End While
        End Function

        Public Shared Function Clean(targets As IEnumerable(Of CleanTarget),
                                     Optional progress As IProgress(Of String) = Nothing) As CleanResult
            Dim res As New CleanResult()
            For Each t In targets
                progress?.Report(t.Title)
                Try
                    If t.Key = "recycle" Then
                        EmptyRecycleBin(res)
                        Continue For
                    End If
                    For Each p In t.Paths
                        CleanFolder(p, t, res)
                    Next
                Catch ex As Exception
                    res.Messages.Add(t.Title & ": " & ex.Message)
                End Try
            Next

            If res.FreedBytes > 0 Then
                Logger.Info($"Privacy clean freed {Fmt.Bytes(res.FreedBytes)} ({res.DeletedFiles} files)")
                Security.EventLogStore.Add("privacy", "Cleanup finished",
                                           $"{Fmt.Bytes(res.FreedBytes)} freed, {res.DeletedFiles} files removed")
            End If
            Return res
        End Function

        Private Shared Sub CleanFolder(folder As String, t As CleanTarget, res As CleanResult)
            If String.IsNullOrEmpty(folder) OrElse Not Directory.Exists(folder) Then Return

            ' never delete the folder we are running from or AVAK's own data
            If folder.StartsWith(AppPaths.Root, StringComparison.OrdinalIgnoreCase) Then Return
            If AppPaths.AppDir.StartsWith(folder, StringComparison.OrdinalIgnoreCase) Then Return

            Dim opts = If(t.Recursive, SearchOption.AllDirectories, SearchOption.TopDirectoryOnly)
            Dim patterns = If(t.FilePatterns.Count > 0, t.FilePatterns, New List(Of String) From {"*"})

            Dim cutoff = DateTime.Now.AddSeconds(-90)

            For Each pat In patterns
                For Each f In SafeFiles(folder, pat, opts).ToList()
                    Try
                        Dim fi As New FileInfo(f)
                        ' a file written seconds ago is probably still open by an installer
                        If fi.LastWriteTime > cutoff Then
                            res.FailedFiles += 1
                            Continue For
                        End If
                        Dim size = fi.Length
                        fi.Attributes = FileAttributes.Normal
                        fi.Delete()
                        res.FreedBytes += size
                        res.DeletedFiles += 1
                    Catch
                        res.FailedFiles += 1
                    End Try
                Next
            Next

            ' remove directories that are now empty (but never the root target itself)
            If t.Recursive Then
                Try
                    For Each d In Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).
                                            OrderByDescending(Function(x) x.Length)
                        Try
                            If Directory.GetFileSystemEntries(d).Length = 0 Then Directory.Delete(d)
                        Catch
                        End Try
                    Next
                Catch
                End Try
            End If
        End Sub

        ' -- recycle bin ------------------------------------------------------

        <Runtime.InteropServices.DllImport("shell32.dll", CharSet:=Runtime.InteropServices.CharSet.Unicode)>
        Private Shared Function SHEmptyRecycleBin(hwnd As IntPtr, pszRootPath As String, dwFlags As UInteger) As Integer
        End Function

        <Runtime.InteropServices.StructLayout(Runtime.InteropServices.LayoutKind.Sequential, CharSet:=Runtime.InteropServices.CharSet.Unicode)>
        Private Structure SHQUERYRBINFO
            Public cbSize As Integer
            Public i64Size As Long
            Public i64NumItems As Long
        End Structure

        <Runtime.InteropServices.DllImport("shell32.dll", CharSet:=Runtime.InteropServices.CharSet.Unicode)>
        Private Shared Function SHQueryRecycleBin(pszRootPath As String, ByRef pSHQueryRBInfo As SHQUERYRBINFO) As Integer
        End Function

        Private Const SHERB_NOCONFIRMATION As UInteger = &H1
        Private Const SHERB_NOPROGRESSUI As UInteger = &H2
        Private Const SHERB_NOSOUND As UInteger = &H4

        Private Shared Sub MeasureRecycleBin(t As CleanTarget)
            Try
                Dim info As New SHQUERYRBINFO()
                info.cbSize = Runtime.InteropServices.Marshal.SizeOf(GetType(SHQUERYRBINFO))
                If SHQueryRecycleBin(Nothing, info) = 0 Then
                    t.SizeBytes = info.i64Size
                    t.FileCount = CInt(Math.Min(Integer.MaxValue, info.i64NumItems))
                End If
            Catch
            End Try
        End Sub

        Private Shared Sub EmptyRecycleBin(res As CleanResult)
            Try
                Dim info As New SHQUERYRBINFO()
                info.cbSize = Runtime.InteropServices.Marshal.SizeOf(GetType(SHQUERYRBINFO))
                Dim before As Long = 0
                Dim items As Long = 0
                If SHQueryRecycleBin(Nothing, info) = 0 Then
                    before = info.i64Size
                    items = info.i64NumItems
                End If
                SHEmptyRecycleBin(IntPtr.Zero, Nothing,
                                  SHERB_NOCONFIRMATION Or SHERB_NOPROGRESSUI Or SHERB_NOSOUND)
                res.FreedBytes += before
                res.DeletedFiles += CInt(Math.Min(Integer.MaxValue, items))
            Catch ex As Exception
                res.Messages.Add("Recycle Bin: " & ex.Message)
            End Try
        End Sub

        ' -- DNS cache --------------------------------------------------------

        Public Shared Function FlushDns() As (Ok As Boolean, Message As String)
            Try
                Dim outp = Elevation.Capture("ipconfig.exe", "/flushdns", 15000)
                Dim ok = outp.IndexOf("Successfully", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                         outp.IndexOf("flushed", StringComparison.OrdinalIgnoreCase) >= 0
                Return (ok, If(ok, "DNS resolver cache flushed.", "ipconfig did not confirm the flush."))
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

        ''' <summary>Clears the "run" MRU list without touching anything else.</summary>
        Public Shared Function ClearRunHistory() As (Ok As Boolean, Message As String)
            Try
                Using k = Registry.CurrentUser.OpenSubKey(
                    "SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU", True)
                    If k Is Nothing Then Return (True, "Nothing to clear.")
                    Dim n = 0
                    For Each v In k.GetValueNames()
                        Try
                            k.DeleteValue(v, False)
                            n += 1
                        Catch
                        End Try
                    Next
                    Return (True, $"Removed {n} entries from the Run history.")
                End Using
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

    End Class

End Namespace
