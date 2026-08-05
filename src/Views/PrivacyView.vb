Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Privacy
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Disk and trace cleanup, plus a look at the hosts file.</summary>
    Public Class PrivacyView
        Inherits ViewBase

        Private _cleanCard As CatCard
        Private _table As CatTable
        Private _analyze As CatButton
        Private _clean As CatButton
        Private _selectAll As CatButton
        Private _found As CatLabel
        Private _bar As CatBar

        Private _toolsCard As CatCard
        Private _flushDns As CatButton
        Private _clearRun As CatButton
        Private _openHosts As CatButton
        Private _hostsTable As CatTable

        Private _targets As List(Of CleanTarget)
        Private _busy As Boolean

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Privacy & cleanup"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Reclaim disk space and clear local traces"
            End Get
        End Property

        Protected Overrides Sub Build()
            _cleanCard = Card("Cleanable items", "sparkle")

            _table = New CatTable With {
                .ShowCheckboxes = True, .RowHeight = 46,
                .EmptyText = "Press Analyze to measure", .EmptyIcon = "search"}
            _table.SetColumns(New CatColumn("Item", 0),
                              New CatColumn("What it is", 0, StringAlignment.Near, True),
                              New CatColumn("Files", 84, StringAlignment.Far, True),
                              New CatColumn("Size", 100, StringAlignment.Far))
            _cleanCard.Controls.Add(_table)

            _found = Lbl("Nothing measured yet.", "body", ThemeManager.FgMuted, _cleanCard)
            _bar = New CatBar()
            _bar.Visible = False
            _cleanCard.Controls.Add(_bar)

            _analyze = Btn("Analyze", ButtonKind.Secondary, "search", Sub() Analyze(), _cleanCard)
            _clean = Btn("Clean now", ButtonKind.Primary, "sparkle", Sub() CleanNow(), _cleanCard)
            _selectAll = Btn("Select all", ButtonKind.Ghost, "check",
                             Sub() _table.SetAllChecked(_table.CheckedRows.Count < _table.RowCount), _cleanCard)

            _toolsCard = Card("Traces & network hygiene", "eye")
            _flushDns = Btn("Flush DNS cache", ButtonKind.Secondary, "refresh",
                            Sub()
                                Dim r = PrivacyCleaner.FlushDns()
                                Say(r.Message, Not r.Ok)
                            End Sub, _toolsCard)
            _clearRun = Btn("Clear Run history", ButtonKind.Secondary, "clock",
                            Sub()
                                Dim r = PrivacyCleaner.ClearRunHistory()
                                Say(r.Message, Not r.Ok)
                            End Sub, _toolsCard)
            _openHosts = Btn("Open hosts file", ButtonKind.Ghost, "file",
                             Sub() HostsGuard.OpenInNotepad(), _toolsCard)

            _hostsTable = New CatTable With {
                .RowHeight = 34, .Sortable = False,
                .EmptyText = "The hosts file has no custom entries", .EmptyIcon = "check-circle"}
            _hostsTable.SetColumns(New CatColumn("Hostname", 0),
                                   New CatColumn("Points to", 150),
                                   New CatColumn("Note", 0, StringAlignment.Near, True))
            _toolsCard.Controls.Add(_hostsTable)

            _targets = PrivacyCleaner.Targets()
            ShowTargets()
        End Sub

        Public Overrides Sub OnActivated()
            RefreshHosts()
        End Sub

        Private Sub ShowTargets()
            _table.SetRows(_targets.Select(
                Function(t) New CatRow(t, t.Title, t.Description,
                                       If(t.Measured, Fmt.Num(t.FileCount), "-"),
                                       If(t.Measured, Fmt.Bytes(t.SizeBytes), "-")) With {
                    .IconName = t.IconName,
                    .Tone = If(t.Risky, ThemeManager.Warn, Color.Empty),
                    .Checked = t.Selected AndAlso (Not t.Measured OrElse t.SizeBytes > 0)}).ToList())
        End Sub

        Private Async Sub Analyze()
            If _busy Then Return
            _busy = True
            _analyze.Busy = True
            _bar.Visible = True
            _bar.SetIndeterminate(True)
            _found.SetText("Measuring...")

            Dim snapshot = _targets
            Await Task.Run(Sub()
                               For Each t In snapshot
                                   PrivacyCleaner.Measure(t)
                               Next
                           End Sub)

            _busy = False
            _analyze.Busy = False
            _bar.SetIndeterminate(False)
            _bar.Visible = False
            ShowTargets()

            Dim total = _targets.Sum(Function(t) t.SizeBytes)
            Dim files = _targets.Sum(Function(t) CLng(t.FileCount))
            _found.SetText($"{Fmt.Bytes(total)} across {Fmt.Num(files)} files can be removed.")
        End Sub

        Private Async Sub CleanNow()
            If _busy Then Return
            Dim picked = _table.CheckedRows.Select(Function(r) CType(r.Tag, CleanTarget)).ToList()
            If picked.Count = 0 Then
                Say("Tick at least one item.", True)
                Return
            End If

            Dim total = picked.Sum(Function(t) t.SizeBytes)
            Dim msg = If(picked.Any(Function(t) Not t.Measured),
                         "Some items have not been measured yet - AVAK will still clean them." & Environment.NewLine & Environment.NewLine,
                         "") &
                      $"{picked.Count} item(s) selected, about {Fmt.Bytes(total)}." & Environment.NewLine &
                      "Deleted files do not go to the Recycle Bin."

            If Not Ask("Clean now?", msg, "Clean", True) Then Return

            _busy = True
            _clean.Busy = True
            _bar.Visible = True
            _bar.SetIndeterminate(True)

            Dim progress As New Progress(Of String)(Sub(s) _found.SetText("Cleaning " & s & "..."))
            Dim result = Await Task.Run(Function() PrivacyCleaner.Clean(picked, progress))

            _busy = False
            _clean.Busy = False
            _bar.SetIndeterminate(False)
            _bar.Visible = False

            For Each t In picked
                t.Measured = False
                t.SizeBytes = 0
                t.FileCount = 0
            Next
            ShowTargets()

            _found.SetText($"Freed {Fmt.Bytes(result.FreedBytes)} - {Fmt.Num(result.DeletedFiles)} files removed" &
                           If(result.FailedFiles > 0, $", {Fmt.Num(result.FailedFiles)} were locked and skipped.", "."))
            Toast.Ok("Cleanup done", $"{Fmt.Bytes(result.FreedBytes)} freed.")
        End Sub

        Private Sub RefreshHosts()
            Dim findings = HostsGuard.Inspect()
            _hostsTable.SetRows(findings.Select(
                Function(f) New CatRow(f, f.Host, f.Address, If(f.Suspicious, f.Why, "Normal redirect")) With {
                    .IconName = If(f.Suspicious, "alert", "check-circle"),
                    .Tone = If(f.Suspicious, ThemeManager.Warn, ThemeManager.Ok)}).ToList())
        End Sub

        Public Overrides Sub Relayout()
            If _cleanCard Is Nothing Then Return
            Dim w = Math.Max(600, Width - Pad * 2)
            Dim topH = CInt(Math.Max(300, (Height - Pad * 3) * 0.58))

            _cleanCard.SetBounds(Pad, Pad, w, topH)
            Dim ct = _cleanCard.ContentTop
            _table.SetBounds(8, ct, w - 16, topH - ct - 62)
            _found.SetBounds(14, topH - 54, w - 410, 22)
            _bar.SetBounds(14, topH - 30, w - 410, 8)
            _selectAll.SetBounds(w - 394, topH - 52, 118, 40)
            _analyze.SetBounds(w - 268, topH - 52, 118, 40)
            _clean.SetBounds(w - 140, topH - 52, 128, 40)

            Dim y = Pad + topH + 16
            Dim h = Math.Max(180, Height - y - Pad)
            _toolsCard.SetBounds(Pad, y, w, h)
            Dim ct2 = _toolsCard.ContentTop
            _flushDns.SetBounds(14, ct2 - 4, 174, 38)
            _clearRun.SetBounds(196, ct2 - 4, 176, 38)
            _openHosts.SetBounds(380, ct2 - 4, 156, 38)
            _hostsTable.SetBounds(8, ct2 + 42, w - 16, h - ct2 - 54)
        End Sub

    End Class

End Namespace
