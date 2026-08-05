Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Scan history, the activity feed and the raw log.</summary>
    Public Class HistoryView
        Inherits ViewBase

        Private _tabs As CatSegment
        Private _card As CatCard
        Private _scans As CatTable
        Private _events As CatTable
        Private _logBox As TextBox
        Private _filter As CatInput
        Private _details As CatButton
        Private _export As CatButton
        Private _clear As CatButton
        Private _openLogs As CatButton

        Public Overrides ReadOnly Property Title As String
            Get
                Return "History"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Everything AVAK has done on this PC"
            End Get
        End Property

        Protected Overrides Sub Build()
            _tabs = New CatSegment()
            _tabs.SetItems("Scans", "Activity", "Log file")
            AddHandler _tabs.SelectionChanged, Sub()
                                                   SwitchTab()
                                                   Relayout()
                                               End Sub
            Controls.Add(_tabs)

            _card = Card("Scans", "history")

            _filter = New CatInput With {.Placeholder = "Filter...", .IconName = "search"}
            AddHandler _filter.TextChangedEx, Sub()
                                                  _scans.Filter = _filter.Value
                                                  _events.Filter = _filter.Value
                                              End Sub
            _card.Controls.Add(_filter)

            _scans = New CatTable With {.RowHeight = 38, .EmptyText = "No scans yet", .EmptyIcon = "scan"}
            _scans.SetColumns(New CatColumn("Started", 150),
                              New CatColumn("Profile", 100, StringAlignment.Near, True),
                              New CatColumn("Files", 100, StringAlignment.Far),
                              New CatColumn("Threats", 90, StringAlignment.Far),
                              New CatColumn("Duration", 100, StringAlignment.Far, True),
                              New CatColumn("Result", 0))
            AddHandler _scans.RowActivated, Sub() ShowScanDetails()
            _card.Controls.Add(_scans)

            _events = New CatTable With {.RowHeight = 36, .EmptyText = "No activity recorded", .EmptyIcon = "activity"}
            _events.SetColumns(New CatColumn("When", 150),
                               New CatColumn("Kind", 110, StringAlignment.Near, True),
                               New CatColumn("What", 260),
                               New CatColumn("Detail", 0, StringAlignment.Near, True))
            _events.Visible = False
            _card.Controls.Add(_events)

            _logBox = New TextBox With {
                .Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical,
                .BorderStyle = BorderStyle.None, .WordWrap = False,
                .BackColor = ThemeManager.Colors.Crust, .ForeColor = ThemeManager.FgMuted,
                .Font = New Font("Consolas", 8.5F), .Visible = False}
            _card.Controls.Add(_logBox)

            _details = Btn("Details", ButtonKind.Secondary, "info", Sub() ShowScanDetails(), _card)
            _export = Btn("Export report", ButtonKind.Ghost, "download", Sub() ExportReport(), _card)
            _clear = Btn("Clear", ButtonKind.Ghost, "trash", Sub() ClearCurrent(), _card)
            _openLogs = Btn("Open log folder", ButtonKind.Ghost, "folder",
                            Sub()
                                Try
                                    Process.Start(New ProcessStartInfo(AppPaths.Logs) With {.UseShellExecute = True})
                                Catch
                                End Try
                            End Sub, _card)
        End Sub

        Public Overrides Sub OnActivated()
            Refresh_()
        End Sub

        Private Sub SwitchTab()
            _scans.Visible = _tabs.SelectedIndex = 0
            _events.Visible = _tabs.SelectedIndex = 1
            _logBox.Visible = _tabs.SelectedIndex = 2
            _details.Visible = _tabs.SelectedIndex = 0
            _export.Visible = _tabs.SelectedIndex = 0
            _filter.Visible = _tabs.SelectedIndex <> 2
            _card.Title = {"Scans", "Activity", "avak-" & DateTime.Now.ToString("yyyyMMdd") & ".log"}(_tabs.SelectedIndex)
            _card.Invalidate()
            Refresh_()
        End Sub

        Private Sub Refresh_()
            If _tabs.SelectedIndex = 0 Then
                _scans.SetRows(HistoryStore.All().Select(
                    Function(r) New CatRow(r, r.StartedAt.ToString("dd MMM yyyy HH:mm"),
                                           r.Profile.ToString(), Fmt.Num(r.FilesScanned),
                                           r.ThreatCount.ToString(), Fmt.Duration(r.Duration),
                                           If(r.Cancelled, "Stopped by user",
                                              If(r.ThreatCount = 0, "Clean",
                                                 SeverityUi.Label(r.WorstSeverity) & " severity"))) With {
                        .IconName = If(r.ThreatCount = 0, "check-circle", SeverityUi.Icon(r.WorstSeverity)),
                        .Tone = If(r.ThreatCount = 0, ThemeManager.Ok, SeverityUi.Color_(r.WorstSeverity))}).ToList())
            ElseIf _tabs.SelectedIndex = 1 Then
                _events.SetRows(EventLogStore.All().Select(
                    Function(ev) New CatRow(ev, ev.At.ToString("dd MMM yyyy HH:mm"), ev.Kind, ev.Title, ev.Detail) With {
                        .IconName = "activity",
                        .Tone = SeverityUi.Color_(ev.Severity)}).ToList())
            Else
                _logBox.Lines = Logger.ReadTail(1500)
                If _logBox.Lines.Length > 0 Then
                    _logBox.SelectionStart = Math.Max(0, _logBox.Text.Length - 1)
                    _logBox.ScrollToCaret()
                End If
            End If
        End Sub

        Private Function SelectedReport() As ScanReport
            Dim r = _scans.SelectedRows.FirstOrDefault()
            Return If(r Is Nothing, Nothing, CType(r.Tag, ScanReport))
        End Function

        Private Sub ShowScanDetails()
            Dim rep = SelectedReport()
            If rep Is Nothing Then
                Say("Select a scan first.", True)
                Return
            End If
            CatDialog.Alert(FindForm(), rep.Profile.ToString() & " scan - " & rep.StartedAt.ToString("dd MMM HH:mm"),
                            BuildReport(rep),
                            If(rep.ThreatCount = 0, DialogTone.Success, DialogTone.Warning))
        End Sub

        Private Shared Function BuildReport(rep As ScanReport) As String
            Dim sb As New StringBuilder()
            sb.AppendLine("Files scanned : " & Fmt.Num(rep.FilesScanned))
            sb.AppendLine("Bytes read    : " & Fmt.Bytes(rep.BytesScanned))
            sb.AppendLine("Skipped       : " & Fmt.Num(rep.SkippedFiles))
            sb.AppendLine("Errors        : " & Fmt.Num(rep.ErrorCount))
            sb.AppendLine("Duration      : " & Fmt.Duration(rep.Duration))
            sb.AppendLine("Roots         : " & String.Join(", ", rep.Roots))
            sb.AppendLine()
            If rep.ThreatCount = 0 Then
                sb.AppendLine("No threats were found.")
            Else
                sb.AppendLine($"{rep.ThreatCount} detection(s):")
                For Each d In rep.Detections.Take(60)
                    sb.AppendLine($"  [{SeverityUi.Label(d.Severity)}] {d.ThreatName}")
                    sb.AppendLine("      " & d.FilePath)
                    If d.Reasons IsNot Nothing AndAlso d.Reasons.Count > 0 Then
                        sb.AppendLine("      " & String.Join("; ", d.Reasons.Take(3)))
                    End If
                Next
                If rep.Detections.Count > 60 Then sb.AppendLine($"  ... and {rep.Detections.Count - 60} more")
            End If
            Return sb.ToString()
        End Function

        Private Sub ExportReport()
            Dim rep = SelectedReport()
            If rep Is Nothing Then
                Say("Select a scan first.", True)
                Return
            End If
            Using dlg As New SaveFileDialog() With {
                .Filter = "Text report (*.txt)|*.txt|JSON (*.json)|*.json",
                .FileName = "avak-scan-" & rep.StartedAt.ToString("yyyyMMdd-HHmm")}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                Try
                    If dlg.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) Then
                        Json.Save(dlg.FileName, rep)
                    Else
                        File.WriteAllText(dlg.FileName,
                            "AVAK scan report" & Environment.NewLine &
                            "================" & Environment.NewLine & Environment.NewLine &
                            BuildReport(rep), Encoding.UTF8)
                    End If
                    Say("Report saved.")
                Catch ex As Exception
                    Say("Export failed: " & ex.Message, True)
                End Try
            End Using
        End Sub

        Private Sub ClearCurrent()
            If _tabs.SelectedIndex = 0 Then
                If Ask("Clear scan history?", "Every stored scan report will be removed.", "Clear", True) Then
                    HistoryStore.Clear()
                    Refresh_()
                End If
            ElseIf _tabs.SelectedIndex = 1 Then
                If Ask("Clear the activity feed?", "The recorded events will be removed.", "Clear", True) Then
                    EventLogStore.Clear()
                    Refresh_()
                End If
            Else
                Say("Log files are rotated automatically - open the folder to remove them.")
            End If
        End Sub

        Public Overrides Sub Relayout()
            If _card Is Nothing Then Return
            Dim w = Math.Max(600, Width - Pad * 2)
            _tabs.SetBounds(Pad, Pad, 360, 38)

            Dim y = Pad + 50
            Dim h = Math.Max(280, Height - y - Pad)
            _card.SetBounds(Pad, y, w, h)
            Dim ct = _card.ContentTop
            _filter.SetBounds(w - 288, 12, 270, 34)

            Dim inner = New Rectangle(8, ct - 6, w - 16, h - ct - 46)
            _scans.Bounds = inner
            _events.Bounds = inner
            _logBox.Bounds = New Rectangle(inner.X + 6, inner.Y, inner.Width - 12, inner.Height)

            _details.SetBounds(12, h - 48, 118, 38)
            _export.SetBounds(138, h - 48, 152, 38)
            _openLogs.SetBounds(298, h - 48, 164, 38)
            _clear.SetBounds(w - 118, h - 48, 106, 38)
        End Sub

    End Class

End Namespace
