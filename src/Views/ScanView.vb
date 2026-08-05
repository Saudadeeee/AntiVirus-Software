Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Scan centre: pick a profile, run it, act on what comes back.</summary>
    Public Class ScanView
        Inherits ViewBase

        Private _modes As CatSegment
        Private _pathsCard As CatCard
        Private _pathsTable As CatTable
        Private _addFolder As CatButton
        Private _addFile As CatButton
        Private _removePath As CatButton
        Private _modeHint As CatLabel

        Private _runCard As CatCard
        Private _ring As CatRing
        Private _bar As CatBar
        Private _current As CatLabel
        Private _counters As CatLabel
        Private _start As CatButton
        Private _stop As CatButton

        Private _results As CatCard
        Private _table As CatTable
        Private _quarantine As CatButton
        Private _delete As CatButton
        Private _openLoc As CatButton
        Private _exclude As CatButton
        Private _filter As CatInput

        Private _cts As CancellationTokenSource
        Private _running As Boolean
        Private ReadOnly _customPaths As New List(Of String)()
        Private _lastReport As ScanReport

        Public Event ScanCompleted(report As ScanReport)

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Scan centre"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Signature, pattern and heuristic analysis"
            End Get
        End Property

        Protected Overrides Sub Build()
            ' -- profile picker
            _modes = New CatSegment()
            _modes.SetItems("Quick", "Full", "Custom", "Running apps")
            AddHandler _modes.SelectionChanged, Sub()
                                                    UpdateMode()
                                                    Relayout()
                                                End Sub
            Controls.Add(_modes)

            _modeHint = Lbl("", "small", ThemeManager.FgMuted)

            _pathsCard = Card("Targets", "folder")
            _pathsTable = New CatTable With {
                .RowHeight = 34, .HeaderHeight = 0, .Sortable = False, .MultiSelect = True,
                .EmptyText = "Add a folder or file to scan", .EmptyIcon = "folder"
            }
            _pathsTable.SetColumns(New CatColumn("Path", 0))
            _pathsCard.Controls.Add(_pathsTable)

            _addFolder = Btn("Add folder", ButtonKind.Secondary, "folder", AddressOf PickFolder, _pathsCard)
            _addFile = Btn("Add file", ButtonKind.Secondary, "file", AddressOf PickFile, _pathsCard)
            _removePath = Btn("Remove", ButtonKind.Ghost, "minus", AddressOf RemovePath, _pathsCard)

            ' -- runner
            _runCard = Card("Progress", "activity")
            _ring = New CatRing With {.Thickness = 12, .ShowPercent = True, .Tone = ThemeManager.Accent}
            _runCard.Controls.Add(_ring)
            _bar = New CatBar()
            _runCard.Controls.Add(_bar)
            _current = Lbl("Ready when you are.", "body", ThemeManager.FgMuted, _runCard)
            _counters = Lbl("", "small", ThemeManager.FgDim, _runCard)
            _start = Btn("Start scan", ButtonKind.Primary, "play", Sub() StartScan(), _runCard)
            _stop = Btn("Stop", ButtonKind.Danger, "stop", Sub() StopScan(), _runCard)
            _stop.Enabled = False

            ' -- results
            _results = Card("Detections", "shield-alert")
            _results.ReserveSubtitle = True
            _filter = New CatInput With {.Placeholder = "Filter results...", .IconName = "search"}
            AddHandler _filter.TextChangedEx, Sub() _table.Filter = _filter.Value
            _results.Controls.Add(_filter)

            _table = New CatTable With {
                .ShowCheckboxes = True,
                .EmptyText = "No threats found",
                .EmptyIcon = "shield-check"
            }
            _table.SetColumns(New CatColumn("Threat", 230),
                              New CatColumn("File", 0),
                              New CatColumn("Severity", 96),
                              New CatColumn("Source", 96, StringAlignment.Near, True),
                              New CatColumn("Size", 84, StringAlignment.Far, True))
            AddHandler _table.RowActivated, Sub() ShowDetails()
            _results.Controls.Add(_table)

            _quarantine = Btn("Quarantine", ButtonKind.Primary, "lock", Sub() QuarantineChecked(), _results)
            _delete = Btn("Delete", ButtonKind.Danger, "trash", Sub() DeleteChecked(), _results)
            _openLoc = Btn("Open location", ButtonKind.Secondary, "folder", Sub() OpenLocation(), _results)
            _exclude = Btn("Trust", ButtonKind.Ghost, "check", Sub() TrustChecked(), _results)

            UpdateMode()
        End Sub

        Public Overrides Sub OnActivated()
            UpdateMode()
        End Sub

        ' -- mode -------------------------------------------------------------

        Private Function CurrentProfile() As ScanProfile
            Select Case _modes.SelectedIndex
                Case 1 : Return ScanProfile.Full
                Case 2 : Return ScanProfile.Custom
                Case 3 : Return ScanProfile.Memory
                Case Else : Return ScanProfile.Quick
            End Select
        End Function

        Private Sub UpdateMode()
            Dim p = CurrentProfile()
            Dim custom = (p = ScanProfile.Custom)
            _pathsCard.Visible = custom
            _addFolder.Visible = custom
            _addFile.Visible = custom
            _removePath.Visible = custom

            Select Case p
                Case ScanProfile.Quick
                    _modeHint.SetText("Temp, Downloads, Desktop, Documents, AppData and the startup folders - executables and scripts only.")
                Case ScanProfile.Full
                    _modeHint.SetText("Every fixed drive, every file type. This takes a while.")
                Case ScanProfile.Custom
                    _modeHint.SetText("Only the folders and files you list below.")
                Case ScanProfile.Memory
                    _modeHint.SetText("Hashes the on-disk image of every running process.")
            End Select

            If custom Then RefreshPaths()
        End Sub

        Private Sub RefreshPaths()
            _pathsTable.SetRows(_customPaths.Select(
                Function(p) New CatRow(p, p) With {
                    .IconName = If(Directory.Exists(p), "folder", "file")}).ToList())
        End Sub

        Private Sub PickFolder(sender As Object, e As EventArgs)
            Using dlg As New FolderBrowserDialog() With {.Description = "Choose a folder to scan", .ShowNewFolderButton = False}
                If dlg.ShowDialog(FindForm()) = DialogResult.OK AndAlso Not String.IsNullOrEmpty(dlg.SelectedPath) Then
                    If Not _customPaths.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase) Then
                        _customPaths.Add(dlg.SelectedPath)
                        RefreshPaths()
                    End If
                End If
            End Using
        End Sub

        Private Sub PickFile(sender As Object, e As EventArgs)
            Using dlg As New OpenFileDialog() With {.Multiselect = True, .Title = "Choose files to scan"}
                If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                    For Each f In dlg.FileNames
                        If Not _customPaths.Contains(f, StringComparer.OrdinalIgnoreCase) Then _customPaths.Add(f)
                    Next
                    RefreshPaths()
                End If
            End Using
        End Sub

        Private Sub RemovePath(sender As Object, e As EventArgs)
            For Each r In _pathsTable.SelectedRows
                _customPaths.Remove(CStr(r.Tag))
            Next
            RefreshPaths()
        End Sub

        ' -- run --------------------------------------------------------------

        ''' <summary>Entry point used by the dashboard and the tray menu.</summary>
        ''' <summary>Scans one folder or file (Explorer right-click, tray, drag-and-drop).</summary>
        Public Sub ScanSpecific(target As String)
            If String.IsNullOrWhiteSpace(target) Then Return
            _customPaths.Clear()
            _customPaths.Add(target)
            _modes.SelectedIndex = 2
            UpdateMode()
            Relayout()
            StartScan()
        End Sub

        Public Sub RunQuickScan()
            _modes.SelectedIndex = 0
            UpdateMode()
            StartScan()
        End Sub

        Public Async Sub StartScan()
            If _running Then Return
            Dim profile = CurrentProfile()

            Dim req As New ScanRequest With {.Profile = profile, .Recursive = True}
            If profile = ScanProfile.Custom Then
                If _customPaths.Count = 0 Then
                    Say("Add at least one folder or file first.", True)
                    Return
                End If
                req.Roots.AddRange(_customPaths)
            ElseIf profile <> ScanProfile.Memory Then
                req.Roots.AddRange(ScanEngine.DefaultRoots(profile))
            End If

            _running = True
            _cts = New CancellationTokenSource()
            _start.Enabled = False
            _start.Busy = True
            _stop.Enabled = True
            _table.ClearRows()
            _ring.SetValueImmediate(0)
            _bar.SetIndeterminate(True)
            _current.SetText("Preparing...")
            _counters.SetText("")

            Dim sw = Stopwatch.StartNew()
            Dim progress As New Progress(Of ScanProgressInfo)(
                Sub(pi)
                    If IsDisposed Then Return
                    If pi.TotalFiles > 0 Then
                        _bar.SetIndeterminate(False)
                        _bar.Value = pi.Percent
                        _ring.Value = pi.Percent
                    End If
                    _current.SetText(If(String.IsNullOrEmpty(pi.CurrentFile),
                                        pi.Phase,
                                        Fmt.ShortPath(pi.CurrentFile, 78)))
                    _counters.SetText($"{Fmt.Num(pi.FilesScanned)} / {Fmt.Num(pi.TotalFiles)} files  -  " &
                                      $"{Fmt.Bytes(pi.BytesScanned)} read  -  {pi.ThreatsFound} threat(s)  -  " &
                                      Fmt.Duration(pi.Elapsed))
                End Sub)

            Dim engine As New ScanEngine()
            Dim report As ScanReport = Nothing
            Try
                report = Await engine.ScanAsync(req, progress, _cts.Token)
            Catch ex As Exception
                Logger.Error("Scan failed", ex)
                Say("Scan failed: " & ex.Message, True)
            End Try

            sw.Stop()
            _running = False
            _start.Busy = False
            _start.Enabled = True
            _stop.Enabled = False
            _bar.SetIndeterminate(False)

            If report Is Nothing Then Return
            _lastReport = report

            _bar.Value = 100
            _ring.Value = 100
            _ring.Tone = If(report.ThreatCount = 0, ThemeManager.Ok, SeverityUi.Color_(report.WorstSeverity))
            _current.SetText(If(report.Cancelled, "Scan stopped.", "Scan finished."))
            _counters.SetText($"{Fmt.Num(report.FilesScanned)} files  -  {Fmt.Bytes(report.BytesScanned)}  -  " &
                              $"{Fmt.Num(report.SkippedFiles)} skipped  -  {Fmt.Duration(report.Duration)}")

            ShowResults(report)

            HistoryStore.Add(report)
            Dim cfg = AppSettings.Current
            cfg.LastScanUtc = DateTime.Now
            cfg.LastScanFiles = report.FilesScanned
            cfg.LastScanThreats = report.ThreatCount
            cfg.TotalScans += 1
            cfg.Save()

            EventLogStore.Add("scan", report.Profile.ToString() & " scan finished",
                              $"{Fmt.Num(report.FilesScanned)} files, {report.ThreatCount} threat(s)",
                              report.WorstSeverity)

            If report.ThreatCount = 0 Then
                Toast.Ok("Scan clean", $"{Fmt.Num(report.FilesScanned)} files checked, nothing found.")
            Else
                Toast.Danger("Threats found", $"{report.ThreatCount} item(s) need your decision.")
                If cfg.OnThreatFound = ThreatAction.Quarantine Then AutoQuarantine(report)
            End If

            RaiseEvent ScanCompleted(report)
        End Sub

        Private Sub StopScan()
            Try
                _cts?.Cancel()
            Catch
            End Try
            _current.SetText("Stopping...")
        End Sub

        Private Sub ShowResults(report As ScanReport)
            _table.SetRows(report.Detections.Select(
                Function(d) New CatRow(d, d.ThreatName, Fmt.ShortPath(d.FilePath, 70),
                                       SeverityUi.Label(d.Severity), d.Source.ToString(), Fmt.Bytes(d.SizeBytes)) With {
                    .IconName = SeverityUi.Icon(d.Severity),
                    .Tone = SeverityUi.Color_(d.Severity)
                }).ToList())
            _results.Subtitle = If(report.ThreatCount = 0, "", $"{report.ThreatCount} detection(s)")
        End Sub

        Private Sub AutoQuarantine(report As ScanReport)
            Dim n = 0
            For Each d In report.Detections
                If d.Severity < Severity.Medium Then Continue For
                If QuarantineManager.Quarantine(d).Ok Then n += 1
            Next
            If n > 0 Then
                AppSettings.Current.TotalThreatsBlocked += n
                AppSettings.Current.Save()
                Toast.Ok("Auto-quarantine", $"{n} file(s) moved to the vault.")
                ShowResults(report)
            End If
        End Sub

        ' -- actions ----------------------------------------------------------

        Private Function Picked() As List(Of Detection)
            Dim checked = _table.CheckedRows.Select(Function(r) CType(r.Tag, Detection)).ToList()
            If checked.Count > 0 Then Return checked
            Return _table.SelectedRows.Select(Function(r) CType(r.Tag, Detection)).ToList()
        End Function

        Private Sub QuarantineChecked()
            Dim items = Picked()
            If items.Count = 0 Then
                Say("Tick the detections you want to quarantine.", True)
                Return
            End If
            If Not Ask("Quarantine " & items.Count & " file(s)?",
                       "Each file is AES-encrypted, moved into the AVAK vault and removed from its original location. You can restore it later.",
                       "Quarantine") Then Return

            Dim ok = 0, needAdmin = False
            Dim errors As New List(Of String)()
            For Each d In items
                Dim r = QuarantineManager.Quarantine(d)
                If r.Ok Then
                    ok += 1
                Else
                    If r.NeedsElevation Then needAdmin = True
                    errors.Add(d.FileName & ": " & r.Message)
                End If
            Next

            If ok > 0 Then
                AppSettings.Current.TotalThreatsBlocked += ok
                AppSettings.Current.Save()
                EventLogStore.Add("quarantine", $"{ok} file(s) quarantined", String.Join(", ", items.Take(3).Select(Function(x) x.FileName)))
            End If

            If _lastReport IsNot Nothing Then
                _lastReport.Detections.RemoveAll(Function(d) items.Contains(d) AndAlso d.Quarantined)
                ShowResults(_lastReport)
            End If

            If errors.Count = 0 Then
                Say($"{ok} file(s) quarantined.")
            ElseIf needAdmin Then
                If Ask("Administrator rights needed",
                       "Some files are protected. Restart AVAK as administrator and try again?", "Restart as admin") Then
                    If Elevation.Relaunch() Then Application.Exit()
                End If
            Else
                CatDialog.Alert(FindForm(), "Some files could not be quarantined",
                                String.Join(Environment.NewLine, errors.Take(8)), DialogTone.Warning)
            End If
        End Sub

        Private Sub DeleteChecked()
            Dim items = Picked()
            If items.Count = 0 Then
                Say("Tick the detections you want to delete.", True)
                Return
            End If
            If Not AskDestructive("Delete " & items.Count & " file(s) permanently?",
                                  "This bypasses the Recycle Bin and cannot be undone. Quarantine is the safer option.",
                                  "Delete forever") Then Return

            Dim ok = 0
            Dim errors As New List(Of String)()
            For Each d In items
                Try
                    File.SetAttributes(d.FilePath, FileAttributes.Normal)
                    File.Delete(d.FilePath)
                    ok += 1
                Catch ex As Exception
                    errors.Add(d.FileName & ": " & ex.Message)
                End Try
            Next

            If _lastReport IsNot Nothing Then
                _lastReport.Detections.RemoveAll(Function(d) items.Contains(d) AndAlso Not File.Exists(d.FilePath))
                ShowResults(_lastReport)
            End If
            EventLogStore.Add("scan", $"{ok} file(s) deleted", "", Severity.Medium)
            Say($"{ok} file(s) deleted." & If(errors.Count > 0, $" {errors.Count} failed.", ""), errors.Count > 0)
        End Sub

        Private Sub TrustChecked()
            Dim items = Picked()
            If items.Count = 0 Then Return
            Dim cfg = AppSettings.Current
            For Each d In items
                If Not cfg.ExcludedPaths.Contains(d.FilePath, StringComparer.OrdinalIgnoreCase) Then
                    cfg.ExcludedPaths.Add(d.FilePath)
                End If
            Next
            cfg.Save()
            If _lastReport IsNot Nothing Then
                _lastReport.Detections.RemoveAll(Function(d) items.Contains(d))
                ShowResults(_lastReport)
            End If
            Say($"{items.Count} path(s) added to the exclusion list.")
        End Sub

        Private Sub OpenLocation()
            Dim items = Picked()
            If items.Count = 0 Then Return
            SystemInfo.ProcessInspector.OpenLocation(items(0).FilePath)
        End Sub

        Private Sub ShowDetails()
            Dim sel = _table.SelectedRows.FirstOrDefault()
            If sel Is Nothing Then Return
            Dim d = CType(sel.Tag, Detection)
            Dim body As New System.Text.StringBuilder()
            body.AppendLine(d.FilePath)
            body.AppendLine()
            body.AppendLine("Severity: " & SeverityUi.Label(d.Severity) & "   Score: " & d.Score)
            body.AppendLine("Source:   " & d.Source.ToString())
            body.AppendLine("Size:     " & Fmt.Bytes(d.SizeBytes))
            If Not String.IsNullOrEmpty(d.Sha256) Then body.AppendLine("SHA-256:  " & d.Sha256)
            body.AppendLine()
            body.AppendLine("Why AVAK flagged it:")
            For Each r In d.Reasons
                body.AppendLine("  - " & r)
            Next
            CatDialog.Alert(FindForm(), d.ThreatName, body.ToString(),
                            If(d.Severity >= Severity.High, DialogTone.Danger, DialogTone.Warning))
        End Sub

        ' -- layout -----------------------------------------------------------

        Public Overrides Sub Relayout()
            If _modes Is Nothing Then Return
            Dim w = Math.Max(600, Width - Pad * 2)

            _modes.SetBounds(Pad, Pad, Math.Min(460, w), 40)
            _modeHint.SetBounds(Pad, Pad + 46, w, 18)

            Dim y = Pad + 74
            Dim custom = _pathsCard.Visible

            If custom Then
                _pathsCard.SetBounds(Pad, y, w, 176)
                Dim ct = _pathsCard.ContentTop
                _pathsTable.SetBounds(8, ct, w - 16 - 150, 176 - ct - 12)
                _addFolder.SetBounds(w - 150, ct, 138, 36)
                _addFile.SetBounds(w - 150, ct + 42, 138, 36)
                _removePath.SetBounds(w - 150, ct + 84, 138, 36)
                y += 190
            End If

            _runCard.SetBounds(Pad, y, w, 178)
            Dim rt = _runCard.ContentTop
            _ring.SetBounds(20, rt - 6, 132, 132)
            _current.SetBounds(170, rt + 4, w - 200, 24)
            _counters.SetBounds(170, rt + 30, w - 200, 20)
            _bar.SetBounds(170, rt + 60, w - 200, 10)
            _start.SetBounds(170, rt + 80, 168, 42)
            _stop.SetBounds(348, rt + 80, 120, 42)

            y += 192
            Dim h = Math.Max(220, Height - y - Pad)
            _results.SetBounds(Pad, y, w, h)
            Dim ct2 = _results.ContentTop
            _filter.SetBounds(w - 268, 14, 250, 34)
            _table.SetBounds(8, ct2, w - 16, h - ct2 - 60)

            Dim by = h - 50
            _quarantine.SetBounds(12, by, 150, 40)
            _delete.SetBounds(172, by, 122, 40)
            _openLoc.SetBounds(304, by, 154, 40)
            _exclude.SetBounds(468, by, 110, 40)
        End Sub

    End Class

End Namespace
