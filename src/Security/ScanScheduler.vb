Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports AVAK.Core

Namespace Security

    ''' <summary>
    ''' Fires scheduled scans. Uses a one-minute UI timer and persists the last run
    ''' so a missed window (machine asleep) is caught the next time AVAK is running.
    ''' </summary>
    Public NotInheritable Class ScanScheduler

        Private Sub New()
        End Sub

        Private Shared _timer As Global.System.Windows.Forms.Timer
        Private Shared _busy As Boolean

        Public Shared Event ScanStarting As EventHandler(Of ScanRequest)
        Public Shared Event ScanFinished As EventHandler(Of ScanReport)

        Public Shared Sub Start()
            If _timer IsNot Nothing Then Return
            _timer = New Global.System.Windows.Forms.Timer() With {.Interval = 60000}
            AddHandler _timer.Tick, AddressOf Tick
            _timer.Start()
            Logger.Info("Scheduler started")
        End Sub

        Public Shared Sub [Stop]()
            _timer?.Stop()
            _timer?.Dispose()
            _timer = Nothing
        End Sub

        Public Shared Function NextRun() As DateTime?
            Dim cfg = AppSettings.Current
            If cfg.Schedule = ScheduleMode.Off Then Return Nothing

            Dim hh As Integer = 20, mm As Integer = 0
            Dim parts = If(cfg.ScheduleTime, "20:00").Split(":"c)
            If parts.Length = 2 Then
                Integer.TryParse(parts(0), hh)
                Integer.TryParse(parts(1), mm)
            End If
            hh = Math.Max(0, Math.Min(23, hh))
            mm = Math.Max(0, Math.Min(59, mm))

            Dim today = DateTime.Today.AddHours(hh).AddMinutes(mm)

            If cfg.Schedule = ScheduleMode.Daily Then
                Return If(today > DateTime.Now, today, today.AddDays(1))
            End If

            ' weekly
            Dim want = CInt(Math.Max(0, Math.Min(6, cfg.ScheduleDayOfWeek)))
            Dim candidate = today
            Dim guard = 0
            While (CInt(candidate.DayOfWeek) <> want OrElse candidate <= DateTime.Now) AndAlso guard < 14
                candidate = candidate.AddDays(1)
                guard += 1
            End While
            Return candidate
        End Function

        Private Shared Sub Tick(sender As Object, e As EventArgs)
            If _busy Then Return
            Dim cfg = AppSettings.Current
            If cfg.Schedule = ScheduleMode.Off Then Return

            Dim due = NextRun()
            If Not due.HasValue Then Return

            ' the window we are "in" is the previous occurrence
            Dim previous = If(cfg.Schedule = ScheduleMode.Daily, due.Value.AddDays(-1), due.Value.AddDays(-7))
            If DateTime.Now < previous Then Return
            If cfg.LastScheduledRun >= previous Then Return

            cfg.LastScheduledRun = DateTime.Now
            cfg.Save()
            RunNow(cfg.ScheduleQuickScan)
        End Sub

        Public Shared Async Sub RunNow(quick As Boolean)
            If _busy Then Return
            _busy = True
            Try
                Dim req As New ScanRequest With {
                    .Profile = If(quick, ScanProfile.Quick, ScanProfile.Full),
                    .Recursive = True
                }
                req.Roots.AddRange(ScanEngine.DefaultRoots(req.Profile))
                RaiseEvent ScanStarting(Nothing, req)

                Ui.Toast.Info("Scheduled scan", If(quick, "Quick scan", "Full scan") & " started automatically.")

                Dim engine As New ScanEngine()
                Dim report = Await engine.ScanAsync(req, Nothing, CancellationToken.None)
                HistoryStore.Add(report)

                Dim cfg = AppSettings.Current
                cfg.LastScanUtc = DateTime.Now
                cfg.LastScanFiles = report.FilesScanned
                cfg.LastScanThreats = report.ThreatCount
                cfg.TotalScans += 1
                cfg.Save()

                EventLogStore.Add("scan", "Scheduled scan finished",
                                  $"{Fmt.Num(report.FilesScanned)} files, {report.ThreatCount} threats",
                                  report.WorstSeverity)

                If report.ThreatCount > 0 Then
                    Ui.Toast.Danger("Scheduled scan", $"{report.ThreatCount} threat(s) found. Open AVAK to review.")
                Else
                    Ui.Toast.Ok("Scheduled scan", "No threats found.")
                End If

                RaiseEvent ScanFinished(Nothing, report)
            Catch ex As Exception
                Logger.Error("Scheduled scan failed", ex)
            Finally
                _busy = False
            End Try
        End Sub

        Public Shared Function Describe() As String
            Dim cfg = AppSettings.Current
            If cfg.Schedule = ScheduleMode.Off Then Return "Scheduled scans are off"
            Dim n = NextRun()
            If Not n.HasValue Then Return "Scheduled scans are off"
            Return "Next run " & n.Value.ToString("ddd dd MMM, HH:mm", CultureInfo.InvariantCulture)
        End Function

    End Class

End Namespace
