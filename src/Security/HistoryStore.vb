Imports AVAK.Core

Namespace Security

    Public Class ScanHistoryFile
        Public Property Reports As New List(Of ScanReport)()
    End Class

    ''' <summary>Rolling store of finished scans (newest first, capped).</summary>
    Public NotInheritable Class HistoryStore

        Private Sub New()
        End Sub

        Private Const MaxEntries As Integer = 120
        Private Shared ReadOnly Gate As New Object()
        Private Shared _file As ScanHistoryFile

        Public Shared Event Changed As EventHandler

        Private Shared ReadOnly Property Store As ScanHistoryFile
            Get
                SyncLock Gate
                    If _file Is Nothing Then
                        _file = Json.Load(Of ScanHistoryFile)(AppPaths.HistoryFile)
                        If _file Is Nothing Then _file = New ScanHistoryFile()
                    End If
                    Return _file
                End SyncLock
            End Get
        End Property

        Public Shared Function All() As List(Of ScanReport)
            SyncLock Gate
                Return Store.Reports.OrderByDescending(Function(r) r.StartedAt).ToList()
            End SyncLock
        End Function

        Public Shared Function Latest() As ScanReport
            SyncLock Gate
                Return Store.Reports.OrderByDescending(Function(r) r.StartedAt).FirstOrDefault()
            End SyncLock
        End Function

        Public Shared Sub Add(r As ScanReport)
            If r Is Nothing Then Return
            SyncLock Gate
                Store.Reports.Add(r)
                If Store.Reports.Count > MaxEntries Then
                    Dim keep = Store.Reports.OrderByDescending(Function(x) x.StartedAt).Take(MaxEntries).ToList()
                    Store.Reports.Clear()
                    Store.Reports.AddRange(keep)
                End If
                Json.Save(AppPaths.HistoryFile, _file)
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Public Shared Sub Clear()
            SyncLock Gate
                Store.Reports.Clear()
                Json.Save(AppPaths.HistoryFile, _file)
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Public Shared Function TotalThreats() As Integer
            SyncLock Gate
                Return Store.Reports.Sum(Function(r) r.ThreatCount)
            End SyncLock
        End Function

    End Class

    Public Class SecurityEvent
        Public Property At As DateTime = DateTime.Now
        ''' <summary>scan | realtime | quarantine | firewall | privacy | system</summary>
        Public Property Kind As String = "system"
        Public Property Title As String = ""
        Public Property Detail As String = ""
        Public Property Severity As Severity = Severity.Clean
    End Class

    Public Class EventLogFile
        Public Property Events As New List(Of SecurityEvent)()
    End Class

    ''' <summary>Human-readable activity feed shown on the dashboard.</summary>
    Public NotInheritable Class EventLogStore

        Private Sub New()
        End Sub

        Private Const MaxEntries As Integer = 400
        Private Shared ReadOnly Gate As New Object()
        Private Shared _file As EventLogFile

        Public Shared Event Changed As EventHandler

        Private Shared ReadOnly Property Store As EventLogFile
            Get
                SyncLock Gate
                    If _file Is Nothing Then
                        _file = Json.Load(Of EventLogFile)(AppPaths.EventsFile)
                        If _file Is Nothing Then _file = New EventLogFile()
                    End If
                    Return _file
                End SyncLock
            End Get
        End Property

        Public Shared Sub Add(e As SecurityEvent)
            If e Is Nothing Then Return
            SyncLock Gate
                Store.Events.Add(e)
                If Store.Events.Count > MaxEntries Then
                    Store.Events.RemoveRange(0, Store.Events.Count - MaxEntries)
                End If
                Json.Save(AppPaths.EventsFile, _file)
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Public Shared Sub Add(kind As String, title As String, detail As String,
                              Optional sev As Severity = Severity.Clean)
            Add(New SecurityEvent With {.Kind = kind, .Title = title, .Detail = detail, .Severity = sev})
        End Sub

        Public Shared Function Recent(n As Integer) As List(Of SecurityEvent)
            SyncLock Gate
                Return Store.Events.OrderByDescending(Function(x) x.At).Take(n).ToList()
            End SyncLock
        End Function

        Public Shared Function All() As List(Of SecurityEvent)
            SyncLock Gate
                Return Store.Events.OrderByDescending(Function(x) x.At).ToList()
            End SyncLock
        End Function

        Public Shared Sub Clear()
            SyncLock Gate
                Store.Events.Clear()
                Json.Save(AppPaths.EventsFile, _file)
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

    End Class

End Namespace
