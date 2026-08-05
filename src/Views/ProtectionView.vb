Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Privacy
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Real-time protection, heuristics, scheduled scans and Defender co-existence.</summary>
    Public Class ProtectionView
        Inherits ViewBase

        Private _rtCard As CatCard
        Private _rtToggle As CatToggle
        Private _rtStatus As CatLabel
        Private _folders As CatTable
        Private _addFolder As CatButton
        Private _removeFolder As CatButton

        Private _engineCard As CatCard
        Private _heurToggle As CatToggle
        Private _sensitivity As CatSegment
        Private _actionCombo As CatCombo
        Private _archiveToggle As CatToggle
        Private _sizeCombo As CatCombo

        Private _schedCard As CatCard
        Private _schedCombo As CatCombo
        Private _schedTime As CatInput
        Private _schedDay As CatCombo
        Private _schedQuick As CatToggle
        Private _schedNext As CatLabel
        Private _runNow As CatButton

        Private _defCard As CatCard
        Private _defRows As CatListRow()
        Private _openDefender As CatButton
        Private _excludeVault As CatButton

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Protection"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Real-time shield, engine tuning and schedules"
            End Get
        End Property

        Protected Overrides Sub Build()
            Dim cfg = AppSettings.Current

            ' -- real time
            _rtCard = Card("Real-time protection", "shield-check", ThemeManager.Ok)
            _rtToggle = New CatToggle("Inspect files as they are created or changed",
                                      "Uses a background watcher on the folders below", cfg.RealtimeProtection)
            AddHandler _rtToggle.CheckedChanged, AddressOf ToggleRealtime
            _rtCard.Controls.Add(_rtToggle)

            _rtStatus = Lbl("", "small", ThemeManager.FgMuted, _rtCard)

            _folders = New CatTable With {
                .RowHeight = 32, .HeaderHeight = 0, .Sortable = False,
                .EmptyText = "No folders are being watched", .EmptyIcon = "folder"}
            _folders.SetColumns(New CatColumn("Folder", 0))
            _rtCard.Controls.Add(_folders)

            _addFolder = Btn("Watch folder", ButtonKind.Secondary, "plus", AddressOf AddWatched, _rtCard)
            _removeFolder = Btn("Remove", ButtonKind.Ghost, "minus", AddressOf RemoveWatched, _rtCard)

            ' -- engine
            _engineCard = Card("Detection engine", "cpu")
            _heurToggle = New CatToggle("Heuristic analysis",
                                        "PE structure, entropy, risky imports and script obfuscation", cfg.HeuristicsEnabled)
            AddHandler _heurToggle.CheckedChanged, Sub()
                                                       cfg.HeuristicsEnabled = _heurToggle.Checked
                                                       cfg.Save()
                                                   End Sub
            _engineCard.Controls.Add(_heurToggle)

            _sensitivity = New CatSegment()
            _sensitivity.SetItems("Lenient", "Balanced", "Aggressive")
            _sensitivity.SelectedIndex = cfg.HeuristicSensitivity - 1
            AddHandler _sensitivity.SelectionChanged, Sub()
                                                          cfg.HeuristicSensitivity = _sensitivity.SelectedIndex + 1
                                                          cfg.Save()
                                                      End Sub
            _engineCard.Controls.Add(_sensitivity)

            _actionCombo = New CatCombo With {.IconName = "shield"}
            _actionCombo.SetItems({"Ask me what to do", "Quarantine automatically", "Report only"})
            _actionCombo.SelectedIndex = CInt(cfg.OnThreatFound)
            AddHandler _actionCombo.SelectionChanged, Sub()
                                                          cfg.OnThreatFound = CType(_actionCombo.SelectedIndex, ThreatAction)
                                                          cfg.Save()
                                                      End Sub
            _engineCard.Controls.Add(_actionCombo)

            _archiveToggle = New CatToggle("Look inside archives",
                                           "Opens ZIP, JAR, APK and Office containers and scans each entry", cfg.ScanArchives)
            AddHandler _archiveToggle.CheckedChanged, Sub()
                                                          cfg.ScanArchives = _archiveToggle.Checked
                                                          cfg.Save()
                                                      End Sub
            _engineCard.Controls.Add(_archiveToggle)

            _sizeCombo = New CatCombo With {.IconName = "database"}
            _sizeCombo.SetItems({"32 MB", "64 MB", "128 MB", "256 MB", "512 MB", "1024 MB"})
            _sizeCombo.SelectedItem = cfg.MaxFileSizeMb & " MB"
            If _sizeCombo.SelectedIndex < 0 Then _sizeCombo.SelectedIndex = 3
            AddHandler _sizeCombo.SelectionChanged, Sub()
                                                        Dim mb = 256
                                                        Integer.TryParse(_sizeCombo.SelectedItem.Replace(" MB", ""), mb)
                                                        cfg.MaxFileSizeMb = mb
                                                        cfg.Save()
                                                    End Sub
            _engineCard.Controls.Add(_sizeCombo)

            ' -- schedule
            _schedCard = Card("Scheduled scans", "clock")
            _schedCombo = New CatCombo With {.IconName = "clock"}
            _schedCombo.SetItems({"Off", "Every day", "Every week"})
            _schedCombo.SelectedIndex = CInt(cfg.Schedule)
            AddHandler _schedCombo.SelectionChanged, Sub()
                                                         cfg.Schedule = CType(_schedCombo.SelectedIndex, ScheduleMode)
                                                         cfg.Save()
                                                         RefreshSchedule()
                                                     End Sub
            _schedCard.Controls.Add(_schedCombo)

            _schedTime = New CatInput With {.Placeholder = "20:00", .Value = cfg.ScheduleTime, .IconName = "clock"}
            AddHandler _schedTime.TextCommitted, Sub()
                                                     cfg.ScheduleTime = _schedTime.Value
                                                     cfg.Save()
                                                     RefreshSchedule()
                                                 End Sub
            _schedCard.Controls.Add(_schedTime)

            _schedDay = New CatCombo With {.IconName = "list"}
            _schedDay.SetItems({"Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"})
            _schedDay.SelectedIndex = cfg.ScheduleDayOfWeek
            AddHandler _schedDay.SelectionChanged, Sub()
                                                       cfg.ScheduleDayOfWeek = _schedDay.SelectedIndex
                                                       cfg.Save()
                                                       RefreshSchedule()
                                                   End Sub
            _schedCard.Controls.Add(_schedDay)

            _schedQuick = New CatToggle("Use a quick scan", "Uncheck for a full drive scan", cfg.ScheduleQuickScan)
            AddHandler _schedQuick.CheckedChanged, Sub()
                                                       cfg.ScheduleQuickScan = _schedQuick.Checked
                                                       cfg.Save()
                                                   End Sub
            _schedCard.Controls.Add(_schedQuick)

            _schedNext = Lbl("", "small", ThemeManager.FgMuted, _schedCard)
            _runNow = Btn("Run now", ButtonKind.Secondary, "play",
                          Sub() ScanScheduler.RunNow(_schedQuick.Checked), _schedCard)

            ' -- defender
            _defCard = Card("Microsoft Defender", "shield")
            _defRows = {New CatListRow(), New CatListRow(), New CatListRow()}
            For Each r In _defRows
                _defCard.Controls.Add(r)
            Next
            _openDefender = Btn("Open Windows Security", ButtonKind.Secondary, "link",
                                Sub() DefenderStatus.OpenWindowsSecurity(), _defCard)
            _excludeVault = Btn("Exclude AVAK vault", ButtonKind.Ghost, "shield-plus",
                                Sub()
                                    Dim r = DefenderStatus.ExcludeQuarantineFolder()
                                    Say(r.Message, Not r.Ok)
                                End Sub, _defCard)
        End Sub

        Public Overrides Sub OnActivated()
            RefreshAll()
        End Sub

        Private Sub RefreshAll()
            Dim cfg = AppSettings.Current
            _rtToggle.SetCheckedSilently(RealtimeMonitor.IsRunning)
            _rtStatus.SetText(If(RealtimeMonitor.IsRunning,
                $"Active - {RealtimeMonitor.WatchedCount} folder(s), {Fmt.Num(RealtimeMonitor.FilesInspected)} files inspected, {RealtimeMonitor.ThreatsBlocked} blocked",
                "Inactive - new downloads are not checked automatically"))
            _rtCard.Accent = If(RealtimeMonitor.IsRunning, ThemeManager.Ok, ThemeManager.Warn)
            _rtCard.Invalidate()

            _folders.SetRows(cfg.WatchedFolders.Select(
                Function(f) New CatRow(f, f) With {
                    .IconName = If(Directory.Exists(f), "folder", "alert"),
                    .Tone = If(Directory.Exists(f), Color.Empty, ThemeManager.Warn)}).ToList())

            RefreshSchedule()
            RefreshDefender()
        End Sub

        Private Sub RefreshSchedule()
            _schedNext.SetText(ScanScheduler.Describe())
            Dim weekly = AppSettings.Current.Schedule = ScheduleMode.Weekly
            _schedDay.Visible = weekly
            Dim off = AppSettings.Current.Schedule = ScheduleMode.Off
            _schedTime.Visible = Not off
            _schedQuick.Visible = Not off
        End Sub

        Private Sub RefreshDefender()
            Dim d = DefenderStatus.Read(True)
            If Not d.Available Then
                SetRow(_defRows(0), False, "Defender status", "Not reporting - another antivirus may be registered", "?")
                SetRow(_defRows(1), True, "Co-existence", "AVAK runs as a second-opinion scanner", "OK")
                SetRow(_defRows(2), True, "Vault exclusion", "Recommended if Defender flags the AVAK quarantine folder", "-")
                Return
            End If
            SetRow(_defRows(0), d.AntivirusEnabled, "Antivirus engine",
                   If(d.AntivirusEnabled, "Enabled", "Disabled"), If(d.AntivirusEnabled, "OK", "Off"))
            SetRow(_defRows(1), d.RealTimeProtectionEnabled, "Defender real-time protection",
                   If(d.RealTimeProtectionEnabled, "Active", "Turned off"), If(d.RealTimeProtectionEnabled, "OK", "Off"))
            SetRow(_defRows(2), d.SignatureAge >= 0 AndAlso d.SignatureAge <= 3, "Defender signatures",
                   If(String.IsNullOrEmpty(d.SignatureVersion), "Unknown version",
                      d.SignatureVersion & If(d.SignatureAge >= 0, $" ({d.SignatureAge} day(s) old)", "")),
                   If(d.SignatureAge <= 3, "Fresh", "Stale"))
        End Sub

        Private Shared Sub SetRow(r As CatListRow, ok As Boolean, title As String, detail As String, trailing As String)
            r.IconName = If(ok, "check-circle", "alert")
            r.Tone = If(ok, ThemeManager.Ok, ThemeManager.Warn)
            r.Title = title
            r.Description = detail
            r.TrailingText = trailing
            r.Invalidate()
        End Sub

        Private Sub ToggleRealtime(sender As Object, e As EventArgs)
            Dim cfg = AppSettings.Current
            If _rtToggle.Checked Then
                If RealtimeMonitor.Start() Then
                    cfg.RealtimeProtection = True
                    cfg.Save()
                    EventLogStore.Add("realtime", "Real-time protection enabled", $"{RealtimeMonitor.WatchedCount} folder(s)")
                    Say("Real-time protection is on.")
                Else
                    _rtToggle.SetCheckedSilently(False)
                    Say("Could not attach to any watched folder. Add one below.", True)
                End If
            Else
                RealtimeMonitor.Stop()
                cfg.RealtimeProtection = False
                cfg.Save()
                EventLogStore.Add("realtime", "Real-time protection disabled", "", Severity.Medium)
            End If
            RefreshAll()
        End Sub

        Private Sub AddWatched(sender As Object, e As EventArgs)
            Using dlg As New FolderBrowserDialog() With {.Description = "Folder to watch in real time"}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                Dim cfg = AppSettings.Current
                If Not cfg.WatchedFolders.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase) Then
                    cfg.WatchedFolders.Add(dlg.SelectedPath)
                    cfg.Save()
                    If RealtimeMonitor.IsRunning Then
                        RealtimeMonitor.Stop()
                        RealtimeMonitor.Start()
                    End If
                End If
                RefreshAll()
            End Using
        End Sub

        Private Sub RemoveWatched(sender As Object, e As EventArgs)
            Dim sel = _folders.SelectedRows
            If sel.Count = 0 Then
                Say("Select a folder first.", True)
                Return
            End If
            Dim cfg = AppSettings.Current
            For Each r In sel
                cfg.WatchedFolders.Remove(CStr(r.Tag))
            Next
            cfg.Save()
            If RealtimeMonitor.IsRunning Then
                RealtimeMonitor.Stop()
                RealtimeMonitor.Start()
            End If
            RefreshAll()
        End Sub

        Public Overrides Sub Relayout()
            If _rtCard Is Nothing Then Return
            Dim w = Math.Max(600, Width - Pad * 2)
            Dim colW = (w - 20) \ 2

            _rtCard.SetBounds(Pad, Pad, colW, 268)
            Dim t1 = _rtCard.ContentTop
            _rtToggle.SetBounds(16, t1 - 6, colW - 32, 44)
            _rtStatus.SetBounds(16, t1 + 40, colW - 32, 18)
            _folders.SetBounds(12, t1 + 62, colW - 24, 268 - t1 - 62 - 54)
            _addFolder.SetBounds(12, 268 - 48, 148, 36)
            _removeFolder.SetBounds(168, 268 - 48, 108, 36)

            _engineCard.SetBounds(Pad + colW + 20, Pad, colW, 268)
            Dim t2 = _engineCard.ContentTop
            _heurToggle.SetBounds(16, t2 - 6, colW - 32, 44)
            _sensitivity.SetBounds(16, t2 + 42, Math.Min(320, colW - 32), 36)
            _actionCombo.SetBounds(16, t2 + 86, colW - 32, 38)
            _archiveToggle.SetBounds(16, t2 + 128, colW - 32, 44)
            _sizeCombo.SetBounds(16, t2 + 176, colW - 32, 38)

            Dim y2 = Pad + 284
            _schedCard.SetBounds(Pad, y2, colW, 254)
            Dim t3 = _schedCard.ContentTop
            _schedCombo.SetBounds(16, t3 - 4, colW - 32, 38)
            _schedTime.SetBounds(16, t3 + 42, 130, 38)
            _schedDay.SetBounds(154, t3 + 42, colW - 170, 38)
            _schedQuick.SetBounds(16, t3 + 86, colW - 32, 44)
            _schedNext.SetBounds(16, t3 + 134, colW - 32, 18)
            _runNow.SetBounds(16, t3 + 156, 130, 38)

            _defCard.SetBounds(Pad + colW + 20, y2, colW, 254)
            Dim t4 = _defCard.ContentTop
            For i = 0 To _defRows.Length - 1
                _defRows(i).SetBounds(8, t4 - 6 + i * 54, colW - 16, 52)
            Next
            _openDefender.SetBounds(14, 254 - 50, 208, 38)
            _excludeVault.SetBounds(230, 254 - 50, 168, 38)
        End Sub

    End Class

End Namespace
