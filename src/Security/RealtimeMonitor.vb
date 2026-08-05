Imports System.Collections.Concurrent
Imports System.IO
Imports System.Threading
Imports AVAK.Core

Namespace Security

    ''' <summary>
    ''' Watches the configured folders and inspects files as they appear or change.
    ''' Events are debounced (a file being written fires many times) and processed
    ''' on a single background worker so the UI thread is never blocked.
    ''' </summary>
    Public NotInheritable Class RealtimeMonitor

        Private Sub New()
        End Sub

        Private Shared ReadOnly Watchers As New List(Of FileSystemWatcher)()
        Private Shared ReadOnly Pending As New ConcurrentDictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)
        Private Shared ReadOnly RecentlyHandled As New ConcurrentDictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)
        Private Shared _worker As Thread
        Private Shared _stop As CancellationTokenSource
        Private Shared _running As Boolean

        Public Shared Event ThreatDetected As EventHandler(Of Detection)
        Public Shared Event StateChanged As EventHandler

        Public Shared ReadOnly Property IsRunning As Boolean
            Get
                Return _running
            End Get
        End Property

        Public Shared ReadOnly Property WatchedCount As Integer
            Get
                SyncLock Watchers
                    Return Watchers.Count
                End SyncLock
            End Get
        End Property

        Private Shared _filesInspected As Long
        Private Shared _threatsBlocked As Long

        ''' <summary>Counters are touched from the watcher thread, so they are read/written atomically.</summary>
        Public Shared ReadOnly Property FilesInspected As Long
            Get
                Return Interlocked.Read(_filesInspected)
            End Get
        End Property

        Public Shared ReadOnly Property ThreatsBlocked As Long
            Get
                Return Interlocked.Read(_threatsBlocked)
            End Get
        End Property

        Public Shared Function Start() As Boolean
            If _running Then Return True
            Dim cfg = AppSettings.Current
            Dim folders = If(cfg.WatchedFolders IsNot Nothing AndAlso cfg.WatchedFolders.Count > 0,
                             cfg.WatchedFolders, AppSettings.DefaultWatchedFolders())

            SyncLock Watchers
                StopWatchers()
                For Each folder In folders
                    Try
                        If Not Directory.Exists(folder) Then Continue For
                        Dim w As New FileSystemWatcher(folder) With {
                            .IncludeSubdirectories = True,
                            .InternalBufferSize = 64 * 1024,
                            .NotifyFilter = NotifyFilters.FileName Or NotifyFilters.LastWrite Or NotifyFilters.CreationTime
                        }
                        AddHandler w.Created, AddressOf OnFsEvent
                        AddHandler w.Changed, AddressOf OnFsEvent
                        AddHandler w.Renamed, AddressOf OnRenamed
                        AddHandler w.Error, AddressOf OnWatcherError
                        w.EnableRaisingEvents = True
                        Watchers.Add(w)
                    Catch ex As Exception
                        Logger.Warn($"Cannot watch '{folder}': {ex.Message}")
                    End Try
                Next
            End SyncLock

            If WatchedCount = 0 Then
                Logger.Warn("Real-time protection could not attach to any folder.")
                Return False
            End If

            _stop = New CancellationTokenSource()
            _worker = New Thread(AddressOf Pump) With {.IsBackground = True, .Name = "AVAK-Realtime"}
            _worker.Start()
            _running = True
            Logger.Info($"Real-time protection ON ({WatchedCount} folders)")
            RaiseEvent StateChanged(Nothing, EventArgs.Empty)
            Return True
        End Function

        Public Shared Sub [Stop]()
            If Not _running Then Return
            _running = False
            Try
                _stop?.Cancel()
            Catch
            End Try
            SyncLock Watchers
                StopWatchers()
            End SyncLock
            Pending.Clear()
            Logger.Info("Real-time protection OFF")
            RaiseEvent StateChanged(Nothing, EventArgs.Empty)
        End Sub

        Private Shared Sub StopWatchers()
            For Each w In Watchers
                Try
                    w.EnableRaisingEvents = False
                    RemoveHandler w.Created, AddressOf OnFsEvent
                    RemoveHandler w.Changed, AddressOf OnFsEvent
                    RemoveHandler w.Renamed, AddressOf OnRenamed
                    RemoveHandler w.Error, AddressOf OnWatcherError
                    w.Dispose()
                Catch
                End Try
            Next
            Watchers.Clear()
        End Sub

        Private Shared Sub OnWatcherError(sender As Object, e As ErrorEventArgs)
            Logger.Warn("FileSystemWatcher error: " & e.GetException().Message)
        End Sub

        Private Shared Sub OnFsEvent(sender As Object, e As FileSystemEventArgs)
            Enqueue(e.FullPath)
        End Sub

        Private Shared Sub OnRenamed(sender As Object, e As RenamedEventArgs)
            Enqueue(e.FullPath)
        End Sub

        Private Shared Sub Enqueue(path As String)
            If String.IsNullOrEmpty(path) Then Return
            ' our own vault must never be re-scanned
            If path.StartsWith(AppPaths.Root, StringComparison.OrdinalIgnoreCase) Then Return
            Pending(path) = DateTime.UtcNow
        End Sub

        ''' <summary>Debounce + inspect loop.</summary>
        Private Shared Sub Pump()
            Dim ct = _stop.Token
            While Not ct.IsCancellationRequested
                Try
                    Thread.Sleep(400)
                    If Pending.IsEmpty Then Continue While

                    Dim now = DateTime.UtcNow
                    Dim ready = Pending.Where(Function(kv) (now - kv.Value).TotalMilliseconds > 700).
                                        Select(Function(kv) kv.Key).Take(64).ToList()

                    For Each path In ready
                        Dim ignored As DateTime
                        Pending.TryRemove(path, ignored)

                        Dim last As DateTime
                        If RecentlyHandled.TryGetValue(path, last) AndAlso (now - last).TotalSeconds < 20 Then Continue For
                        RecentlyHandled(path) = now

                        Inspect(path)
                    Next

                    ' keep the dedup map small
                    If RecentlyHandled.Count > 4000 Then
                        For Each kv In RecentlyHandled.Where(Function(x) (now - x.Value).TotalMinutes > 5).ToList()
                            Dim ignored As DateTime
                            RecentlyHandled.TryRemove(kv.Key, ignored)
                        Next
                    End If
                Catch ex As Exception
                    Logger.Warn("Realtime pump: " & ex.Message)
                End Try
            End While
        End Sub

        Private Shared Sub Inspect(path As String)
            Try
                Dim cfg = AppSettings.Current
                Dim fi As New FileInfo(path)
                If Not fi.Exists OrElse fi.Length = 0 Then Return
                If fi.Length > CLng(cfg.MaxFileSizeMb) * 1024L * 1024L Then Return

                Interlocked.Increment(_filesInspected)
                Dim det = ScanEngine.InspectFile(path, fi, cfg)
                If det Is Nothing Then Return

                Interlocked.Increment(_threatsBlocked)
                Logger.Warn($"Realtime hit: {det.ThreatName} in {path}")

                If cfg.OnThreatFound = ThreatAction.Quarantine Then
                    Dim r = QuarantineManager.Quarantine(det)
                    If r.Ok Then
                        cfg.TotalThreatsBlocked += 1
                        cfg.Save()
                    End If
                End If

                EventLogStore.Add(New SecurityEvent With {
                    .Kind = "realtime",
                    .Title = det.ThreatName,
                    .Detail = path,
                    .Severity = det.Severity})

                RaiseEvent ThreatDetected(Nothing, det)
            Catch ex As Exception
                Logger.Warn("Realtime inspect failed for " & path & ": " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
