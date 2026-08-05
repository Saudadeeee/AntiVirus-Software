Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>The encrypted vault: inspect, restore or destroy what AVAK took away.</summary>
    Public Class QuarantineView
        Inherits ViewBase

        Private _summary As CatCard
        Private _stats As CatStat()
        Private _table As CatTable
        Private _filter As CatInput
        Private _restore As CatButton
        Private _delete As CatButton
        Private _purge As CatButton
        Private _details As CatButton
        Private _openFolder As CatButton

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Quarantine"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "AES-encrypted vault - nothing here can execute"
            End Get
        End Property

        Protected Overrides Sub Build()
            _stats = {
                New CatStat("Items", "lock", ThemeManager.Colors.Peach),
                New CatStat("Vault size", "database", ThemeManager.Colors.Sapphire),
                New CatStat("Highest severity", "shield-alert", ThemeManager.Colors.Red)
            }
            For Each s In _stats
                Controls.Add(s)
            Next

            _summary = Card("Vault contents", "lock")

            _filter = New CatInput With {.Placeholder = "Filter by name or threat...", .IconName = "search"}
            AddHandler _filter.TextChangedEx, Sub() _table.Filter = _filter.Value
            _summary.Controls.Add(_filter)

            _table = New CatTable With {
                .ShowCheckboxes = True,
                .EmptyText = "The vault is empty - that is a good thing",
                .EmptyIcon = "shield-check"}
            _table.SetColumns(New CatColumn("File", 0),
                              New CatColumn("Threat", 210),
                              New CatColumn("Severity", 96),
                              New CatColumn("Size", 84, StringAlignment.Far, True),
                              New CatColumn("Quarantined", 132, StringAlignment.Near, True))
            AddHandler _table.RowActivated, Sub() ShowDetails()
            _summary.Controls.Add(_table)

            _restore = Btn("Restore", ButtonKind.Secondary, "upload", Sub() RestoreChecked(), _summary)
            _delete = Btn("Delete", ButtonKind.Danger, "trash", Sub() DeleteChecked(), _summary)
            _purge = Btn("Empty vault", ButtonKind.Ghost, "flame", Sub() PurgeAll(), _summary)
            _details = Btn("Details", ButtonKind.Ghost, "info", Sub() ShowDetails(), _summary)
            _openFolder = Btn("Open folder", ButtonKind.Ghost, "folder",
                              Sub()
                                  Try
                                      Process.Start(New ProcessStartInfo(AppPaths.Quarantine) With {.UseShellExecute = True})
                                  Catch
                                  End Try
                              End Sub, _summary)
        End Sub

        Public Overrides Sub OnActivated()
            QuarantineManager.Reconcile()
            Refresh_()
        End Sub

        Private Sub Refresh_()
            Dim items = QuarantineManager.All()
            _stats(0).SetValue(items.Count.ToString(), If(items.Count = 0, "nothing stored", "restorable"))
            _stats(1).SetValue(Fmt.Bytes(QuarantineManager.TotalBytes), AppPaths.Quarantine)
            Dim worst = If(items.Count = 0, Severity.Clean, items.Max(Function(i) i.Severity))
            _stats(2).SetValue(SeverityUi.Label(worst), If(items.Count = 0, "-", items.Count & " item(s)"))
            _stats(2).Tone = SeverityUi.Color_(worst)

            _table.SetRows(items.Select(
                Function(q) New CatRow(q, q.FileName, q.ThreatName, SeverityUi.Label(q.Severity),
                                       Fmt.Bytes(q.SizeBytes), q.QuarantinedAt.ToString("dd MMM yyyy HH:mm")) With {
                    .IconName = SeverityUi.Icon(q.Severity),
                    .Tone = SeverityUi.Color_(q.Severity)}).ToList())
        End Sub

        Private Function Picked() As List(Of QuarantineItem)
            Dim checked = _table.CheckedRows.Select(Function(r) CType(r.Tag, QuarantineItem)).ToList()
            If checked.Count > 0 Then Return checked
            Return _table.SelectedRows.Select(Function(r) CType(r.Tag, QuarantineItem)).ToList()
        End Function

        Private Sub RestoreChecked()
            Dim items = Picked()
            If items.Count = 0 Then
                Say("Select the items you want to restore.", True)
                Return
            End If
            If Not Ask("Restore " & items.Count & " file(s)?",
                       "The original file will be written back to disk exactly as it was, including anything malicious in it. " &
                       "Only do this if you are certain the detection was wrong.",
                       "Restore anyway", True) Then Return

            Dim ok = 0
            Dim errors As New List(Of String)()
            For Each q In items
                Dim r = QuarantineManager.Restore(q.Id)
                If r.Ok Then
                    ok += 1
                    ' a restored file should not be re-flagged the second it lands
                    Dim cfg = AppSettings.Current
                    If Not cfg.ExcludedPaths.Contains(q.OriginalPath, StringComparer.OrdinalIgnoreCase) Then
                        cfg.ExcludedPaths.Add(q.OriginalPath)
                        cfg.Save()
                    End If
                Else
                    errors.Add(q.FileName & ": " & r.Message)
                End If
            Next
            EventLogStore.Add("quarantine", $"{ok} file(s) restored", "", Severity.Medium)
            Refresh_()
            If errors.Count = 0 Then
                Say($"{ok} file(s) restored and added to the exclusion list.")
            Else
                CatDialog.Alert(FindForm(), "Some restores failed",
                                String.Join(Environment.NewLine, errors.Take(8)), DialogTone.Warning)
            End If
        End Sub

        Private Sub DeleteChecked()
            Dim items = Picked()
            If items.Count = 0 Then
                Say("Select the items you want to delete.", True)
                Return
            End If
            If Not AskDestructive("Delete " & items.Count & " item(s) forever?",
                                  "The vault copy is overwritten with random bytes and then unlinked. This cannot be undone.",
                                  "Delete forever") Then Return

            Dim ok = 0
            For Each q In items
                If QuarantineManager.DeleteForever(q.Id).Ok Then ok += 1
            Next
            EventLogStore.Add("quarantine", $"{ok} item(s) destroyed", "")
            Refresh_()
            Say($"{ok} item(s) permanently deleted.")
        End Sub

        Private Sub PurgeAll()
            If QuarantineManager.Count = 0 Then
                Say("The vault is already empty.")
                Return
            End If
            If Not Ask("Empty the whole vault?",
                       $"All {QuarantineManager.Count} item(s) will be shredded. This cannot be undone.",
                       "Empty vault", True) Then Return
            Dim n = QuarantineManager.Purge()
            EventLogStore.Add("quarantine", "Vault emptied", $"{n} item(s)")
            Refresh_()
            Say($"{n} item(s) removed.")
        End Sub

        Private Sub ShowDetails()
            Dim items = Picked()
            If items.Count = 0 Then Return
            Dim q = items(0)
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("Original location:")
            sb.AppendLine(q.OriginalPath)
            sb.AppendLine()
            sb.AppendLine("Threat:      " & q.ThreatName)
            sb.AppendLine("Severity:    " & SeverityUi.Label(q.Severity))
            sb.AppendLine("Detected by: " & q.Source.ToString())
            sb.AppendLine("Size:        " & Fmt.Bytes(q.SizeBytes))
            sb.AppendLine("SHA-256:     " & q.Sha256)
            sb.AppendLine("Vault file:  " & q.VaultFile)
            If q.Reasons IsNot Nothing AndAlso q.Reasons.Count > 0 Then
                sb.AppendLine()
                sb.AppendLine("Why it was flagged:")
                For Each r In q.Reasons
                    sb.AppendLine("  - " & r)
                Next
            End If
            CatDialog.Alert(FindForm(), q.FileName, sb.ToString(), DialogTone.Warning)
        End Sub

        Public Overrides Sub Relayout()
            If _summary Is Nothing Then Return
            Dim w = Math.Max(600, Width - Pad * 2)
            Dim tileW = (w - 32) \ 3
            For i = 0 To _stats.Length - 1
                _stats(i).SetBounds(Pad + i * (tileW + 16), Pad, tileW, 104)
            Next

            Dim y = Pad + 120
            Dim h = Math.Max(260, Height - y - Pad)
            _summary.SetBounds(Pad, y, w, h)
            Dim ct = _summary.ContentTop
            _filter.SetBounds(w - 288, 14, 270, 34)
            _table.SetBounds(8, ct, w - 16, h - ct - 60)

            Dim by = h - 50
            _restore.SetBounds(12, by, 128, 40)
            _delete.SetBounds(150, by, 118, 40)
            _details.SetBounds(278, by, 110, 40)
            _openFolder.SetBounds(396, by, 140, 40)
            _purge.SetBounds(w - 152, by, 140, 40)
        End Sub

    End Class

End Namespace
