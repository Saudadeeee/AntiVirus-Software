Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Network
Imports AVAK.Privacy
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Landing page: one protection score, the facts behind it, and quick actions.</summary>
    Public Class DashboardView
        Inherits ViewBase

        Private _hero As CatCard
        Private _ring As CatRing
        Private _statusBig As CatLabel
        Private _statusSub As CatLabel
        Private _quickScan As CatButton
        Private _fixAll As CatButton

        Private _tiles As CatStat()
        Private _checks As CatCard
        Private _checkRows As CatListRow()
        Private _activity As CatCard
        Private _activityTable As CatTable

        Private _refresh As Global.System.Windows.Forms.Timer

        Public Event RequestNavigate(key As String)
        Public Event RequestQuickScan()

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Dashboard"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Overall protection status for this PC"
            End Get
        End Property

        Protected Overrides Sub Build()
            _hero = New CatCard With {.Elevated = True}
            Controls.Add(_hero)

            _ring = New CatRing With {.Thickness = 14, .ShowPercent = False, .CenterIcon = "shield-check"}
            _hero.Controls.Add(_ring)

            _statusBig = Lbl("Checking...", "display", Nothing, _hero)
            _statusSub = Lbl("", "body", ThemeManager.FgMuted, _hero)

            _quickScan = Btn("Quick scan", ButtonKind.Primary, "scan",
                             Sub() RaiseEvent RequestQuickScan(), _hero)
            _fixAll = Btn("Fix issues", ButtonKind.Secondary, "sparkle",
                          Sub() FixIssues(), _hero)

            _tiles = {
                New CatStat("Threats blocked", "shield-alert", ThemeManager.Colors.Red),
                New CatStat("In quarantine", "lock", ThemeManager.Colors.Peach),
                New CatStat("Last scan", "clock", ThemeManager.Colors.Sapphire),
                New CatStat("Signatures", "database", ThemeManager.Colors.Green)
            }
            For Each t In _tiles
                t.Clickable = True
                Controls.Add(t)
            Next
            AddHandler _tiles(0).Click, Sub() RaiseEvent RequestNavigate("history")
            AddHandler _tiles(1).Click, Sub() RaiseEvent RequestNavigate("quarantine")
            AddHandler _tiles(2).Click, Sub() RaiseEvent RequestNavigate("history")
            AddHandler _tiles(3).Click, Sub() RaiseEvent RequestNavigate("settings")

            _checks = Card("Security checklist", "list")
            _checkRows = {
                New CatListRow With {.Clickable = True},
                New CatListRow With {.Clickable = True},
                New CatListRow With {.Clickable = True},
                New CatListRow With {.Clickable = True}
            }
            For Each r In _checkRows
                _checks.Controls.Add(r)
            Next
            AddHandler _checkRows(0).Click, Sub() RaiseEvent RequestNavigate("protection")
            AddHandler _checkRows(1).Click, Sub() RaiseEvent RequestNavigate("network")
            AddHandler _checkRows(2).Click, Sub() DefenderStatus.OpenWindowsSecurity()
            AddHandler _checkRows(3).Click, Sub() RaiseEvent RequestNavigate("protection")

            _activity = Card("Recent activity", "activity")
            _activityTable = New CatTable With {
                .RowHeight = 38, .HeaderHeight = 0, .Sortable = False,
                .EmptyText = "Nothing has happened yet", .EmptyIcon = "clock"
            }
            _activityTable.SetColumns(New CatColumn("When", 96, StringAlignment.Near, True),
                                      New CatColumn("What", 0),
                                      New CatColumn("Detail", 0, StringAlignment.Near, True))
            _activity.Controls.Add(_activityTable)

            _refresh = New Global.System.Windows.Forms.Timer() With {.Interval = 5000}
            AddHandler _refresh.Tick, Sub() Refresh_()
        End Sub

        Public Overrides Sub OnActivated()
            Refresh_()
            _refresh?.Start()
        End Sub

        Public Overrides Sub OnDeactivated()
            _refresh?.Stop()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _refresh IsNot Nothing Then
                _refresh.Stop()
                _refresh.Dispose()
                _refresh = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        ' -- state ------------------------------------------------------------

        Private Structure Health
            Public Score As Integer
            Public Realtime As Boolean
            Public Firewall As FirewallState
            Public Defender As DefenderInfo
            Public Quarantine As Integer
            Public LastScan As DateTime
            Public Issues As Integer
        End Structure

        Private Function Assess() As Health
            Dim cfg = AppSettings.Current
            Dim h As New Health()
            h.Realtime = RealtimeMonitor.IsRunning
            h.Firewall = FirewallManager.Read()
            h.Defender = DefenderStatus.Read()
            h.Quarantine = QuarantineManager.Count
            h.LastScan = cfg.LastScanUtc

            Dim score = 100
            If Not h.Realtime Then
                score -= 22
                h.Issues += 1
            End If
            If h.Firewall.Available AndAlso Not h.Firewall.AllOn Then
                score -= 22
                h.Issues += 1
            End If
            If h.Defender.Available AndAlso Not h.Defender.Healthy Then
                score -= 16
                h.Issues += 1
            End If
            If h.LastScan = DateTime.MinValue Then
                score -= 20
                h.Issues += 1
            ElseIf (DateTime.Now - h.LastScan).TotalDays > 7 Then
                score -= 14
                h.Issues += 1
            End If
            If h.Quarantine > 0 Then score -= Math.Min(10, h.Quarantine)

            h.Score = Math.Max(0, Math.Min(100, score))
            Return h
        End Function

        Private Sub Refresh_()
            If IsDisposed Then Return
            Dim cfg = AppSettings.Current
            Dim h = Assess()

            Dim accent As Color
            Dim headline As String
            Dim icon As String
            If h.Score >= 90 Then
                accent = ThemeManager.Ok
                headline = "You're protected"
                icon = "shield-check"
            ElseIf h.Score >= 65 Then
                accent = ThemeManager.Warn
                headline = "Needs attention"
                icon = "shield-alert"
            Else
                accent = ThemeManager.Danger
                headline = "At risk"
                icon = "shield-alert"
            End If

            _ring.Tone = accent
            _ring.CenterIcon = icon
            _ring.CenterText = h.Score.ToString()
            _ring.CenterSub = "protection score"
            _ring.Value = h.Score

            _statusBig.Tone = accent
            _statusBig.SetText(headline)
            _statusSub.SetText(If(h.Issues = 0,
                                  "Every check passed. Last scan " & Fmt.Ago(h.LastScan) & ".",
                                  $"{h.Issues} item(s) need your attention."))
            _fixAll.Enabled = h.Issues > 0

            _tiles(0).SetValue(Fmt.Num(cfg.TotalThreatsBlocked), $"{HistoryStore.TotalThreats()} found in scans")
            _tiles(1).SetValue(h.Quarantine.ToString(), Fmt.Bytes(QuarantineManager.TotalBytes) & " stored")
            _tiles(2).SetValue(Fmt.Ago(h.LastScan), $"{Fmt.Num(cfg.LastScanFiles)} files last time")
            _tiles(3).SetValue(SignatureDatabase.Version,
                               $"{SignatureDatabase.HashCount} hashes / {SignatureDatabase.PatternCount} patterns")

            SetCheck(_checkRows(0), h.Realtime, "Real-time protection",
                     If(h.Realtime, $"Watching {RealtimeMonitor.WatchedCount} folder(s)", "Off - files are not checked as they arrive"))

            Dim fwOk = h.Firewall.Available AndAlso h.Firewall.AllOn
            SetCheck(_checkRows(1), fwOk, "Windows Firewall",
                     If(h.Firewall.Available, h.Firewall.Summary, "State unavailable"))

            If h.Defender.Available Then
                SetCheck(_checkRows(2), h.Defender.Healthy, "Microsoft Defender",
                         If(h.Defender.Healthy,
                            "Active, signatures " & If(h.Defender.SignatureAge >= 0, h.Defender.SignatureAge & " day(s) old", "current"),
                            "Real-time protection is off"))
            Else
                SetCheck(_checkRows(2), True, "Microsoft Defender", "Not reporting (another AV may be in charge)")
            End If

            Dim scanOk = h.LastScan <> DateTime.MinValue AndAlso (DateTime.Now - h.LastScan).TotalDays <= 7
            SetCheck(_checkRows(3), scanOk, "Recent scan",
                     If(h.LastScan = DateTime.MinValue, "This PC has never been scanned",
                        "Last scan " & Fmt.Ago(h.LastScan) & " - " & ScanScheduler.Describe()))

            Dim rows = EventLogStore.Recent(40).Select(
                Function(ev) New CatRow(ev, ev.At.ToString("dd MMM HH:mm"), ev.Title, ev.Detail) With {
                    .IconName = IconForKind(ev.Kind),
                    .Tone = SeverityUi.Color_(ev.Severity)
                }).ToList()
            _activityTable.SetRows(rows)
        End Sub

        Private Shared Function IconForKind(kind As String) As String
            Select Case kind
                Case "scan" : Return "scan"
                Case "realtime" : Return "shield-alert"
                Case "quarantine" : Return "lock"
                Case "firewall" : Return "shield"
                Case "privacy" : Return "sparkle"
                Case "network" : Return "globe"
                Case Else : Return "info"
            End Select
        End Function

        Private Shared Sub SetCheck(row As CatListRow, ok As Boolean, title As String, detail As String)
            row.IconName = If(ok, "check-circle", "alert")
            row.Tone = If(ok, ThemeManager.Ok, ThemeManager.Warn)
            row.Title = title
            row.Description = detail
            row.TrailingText = If(ok, "OK", "Fix")
            row.Invalidate()
        End Sub

        Private Sub FixIssues()
            Dim h = Assess()
            Dim done As New List(Of String)()

            If Not h.Realtime Then
                If RealtimeMonitor.Start() Then
                    AppSettings.Current.RealtimeProtection = True
                    AppSettings.Current.Save()
                    done.Add("real-time protection enabled")
                End If
            End If

            If h.Firewall.Available AndAlso Not h.Firewall.AllOn Then
                If Ask("Turn the firewall back on?",
                       "AVAK will run 'netsh advfirewall set allprofiles state on'. Windows will ask for administrator rights.",
                       "Turn on") Then
                    Dim r = FirewallManager.SetAll(True)
                    If r.Ok Then done.Add("firewall enabled")
                End If
            End If

            If h.LastScan = DateTime.MinValue OrElse (DateTime.Now - h.LastScan).TotalDays > 7 Then
                RaiseEvent RequestQuickScan()
                Return
            End If

            Refresh_()
            If done.Count > 0 Then
                Say("Fixed: " & String.Join(", ", done) & ".")
            Else
                Say("Nothing could be fixed automatically - open the relevant page.", True)
            End If
        End Sub

        ' -- layout -----------------------------------------------------------

        Public Overrides Sub Relayout()
            If _hero Is Nothing Then Return
            Dim w = Math.Max(560, Width - Pad * 2)

            _hero.SetBounds(Pad, Pad, w, 208)
            _ring.SetBounds(24, 22, 164, 164)
            _statusBig.SetBounds(210, 46, w - 240, 42)
            _statusSub.SetBounds(210, 90, w - 240, 24)
            _quickScan.SetBounds(210, 126, 168, 44)
            _fixAll.SetBounds(390, 126, 150, 44)

            Dim tileW = (w - 3 * 16) \ 4
            For i = 0 To _tiles.Length - 1
                _tiles(i).SetBounds(Pad + i * (tileW + 16), Pad + 224, tileW, 104)
            Next

            Dim rowY = Pad + 348
            Dim leftW = CInt(w * 0.52)
            Dim rightW = w - leftW - 16
            Dim h = Math.Max(240, Height - rowY - Pad)

            _checks.SetBounds(Pad, rowY, leftW, h)
            Dim inner = _checks.ContentTop
            For i = 0 To _checkRows.Length - 1
                _checkRows(i).SetBounds(8, inner + i * 58, leftW - 16, 56)
            Next

            _activity.SetBounds(Pad + leftW + 16, rowY, rightW, h)
            _activityTable.SetBounds(8, _activity.ContentTop, rightW - 16, h - _activity.ContentTop - 12)
        End Sub

    End Class

End Namespace
