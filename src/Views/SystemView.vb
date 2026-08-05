Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.SystemInfo
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Live telemetry, hardware facts, running processes and autoruns.</summary>
    Public Class SystemView
        Inherits ViewBase

        Private _cpuCard As CatCard
        Private _cpuSpark As CatSpark
        Private _ramCard As CatCard
        Private _ramSpark As CatSpark
        Private _specCard As CatCard
        Private _specText As CatLabel
        Private _diskCard As CatCard
        Private _diskTable As CatTable

        Private _tabs As CatSegment
        Private _listCard As CatCard
        Private _procTable As CatTable
        Private _startupTable As CatTable
        Private _filter As CatInput
        Private _btnA As CatButton
        Private _btnB As CatButton
        Private _btnC As CatButton
        Private _btnRefresh As CatButton

        Private _timer As Global.System.Windows.Forms.Timer

        Public Overrides ReadOnly Property Title As String
            Get
                Return "System"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Live performance, hardware, processes and autoruns"
            End Get
        End Property

        Protected Overrides Sub Build()
            _cpuCard = Card("CPU", "cpu", ThemeManager.Colors.Sapphire)
            _cpuCard.ReserveSubtitle = True
            _cpuSpark = New CatSpark With {.Tone = ThemeManager.Colors.Sapphire, .Caption = "Utilisation"}
            _cpuCard.Controls.Add(_cpuSpark)

            _ramCard = Card("Memory", "memory", ThemeManager.Colors.Mauve)
            _ramCard.ReserveSubtitle = True
            _ramSpark = New CatSpark With {.Tone = ThemeManager.Colors.Mauve, .Caption = "In use"}
            _ramCard.Controls.Add(_ramSpark)

            _specCard = Card("This machine", "server")
            _specCard.ReserveSubtitle = True
            _specText = Lbl("", "small", ThemeManager.FgMuted, _specCard)
            _specText.Wrap = True
            _specText.VAlign = StringAlignment.Near

            _diskCard = Card("Storage", "disk")
            _diskTable = New CatTable With {
                .RowHeight = 34, .Sortable = False, .EmptyText = "No drives found", .EmptyIcon = "disk"}
            _diskTable.SetColumns(New CatColumn("Drive", 0),
                                  New CatColumn("Used", 84, StringAlignment.Far),
                                  New CatColumn("Free", 84, StringAlignment.Far, True))
            _diskCard.Controls.Add(_diskTable)

            _tabs = New CatSegment()
            _tabs.SetItems("Processes", "Startup items")
            AddHandler _tabs.SelectionChanged, Sub()
                                                   SwitchTab()
                                                   Relayout()
                                               End Sub
            Controls.Add(_tabs)

            _listCard = Card("", "list")
            _filter = New CatInput With {.Placeholder = "Filter...", .IconName = "search"}
            AddHandler _filter.TextChangedEx, Sub()
                                                  _procTable.Filter = _filter.Value
                                                  _startupTable.Filter = _filter.Value
                                              End Sub
            _listCard.Controls.Add(_filter)

            _procTable = New CatTable With {.RowHeight = 34, .EmptyText = "No processes", .EmptyIcon = "cpu"}
            _procTable.SetColumns(New CatColumn("Process", 0),
                                  New CatColumn("PID", 70, StringAlignment.Far, True),
                                  New CatColumn("Memory", 100, StringAlignment.Far),
                                  New CatColumn("Threads", 80, StringAlignment.Far, True),
                                  New CatColumn("Trust", 100),
                                  New CatColumn("Publisher", 200, StringAlignment.Near, True))
            _listCard.Controls.Add(_procTable)

            _startupTable = New CatTable With {.RowHeight = 36, .EmptyText = "Nothing runs at startup", .EmptyIcon = "power"}
            _startupTable.SetColumns(New CatColumn("Name", 0),
                                     New CatColumn("Location", 170, StringAlignment.Near, True),
                                     New CatColumn("Status", 96),
                                     New CatColumn("Publisher", 180, StringAlignment.Near, True),
                                     New CatColumn("Command", 0, StringAlignment.Near, True))
            _startupTable.Visible = False
            _listCard.Controls.Add(_startupTable)

            _btnA = Btn("End process", ButtonKind.Danger, "ban", Sub() PrimaryAction(), _listCard)
            _btnB = Btn("Scan image", ButtonKind.Secondary, "scan", Sub() SecondaryAction(), _listCard)
            _btnC = Btn("Open location", ButtonKind.Ghost, "folder", Sub() TertiaryAction(), _listCard)
            _btnRefresh = Btn("Refresh", ButtonKind.Ghost, "refresh", Sub() RefreshLists(), _listCard)

            _timer = New Global.System.Windows.Forms.Timer() With {.Interval = 1000}
            AddHandler _timer.Tick, Sub() Sample()

            SwitchTab()
        End Sub

        Public Overrides Sub OnActivated()
            LoadSpecs()
            RefreshLists()
            Sample()
            _timer?.Start()
        End Sub

        Public Overrides Sub OnDeactivated()
            _timer?.Stop()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _timer IsNot Nothing Then
                _timer.Stop()
                _timer.Dispose()
                _timer = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private Sub Sample()
            Dim s = PerfMonitor.Read()
            _cpuSpark.Push(s.CpuPercent)
            _ramSpark.Push(s.RamPercent)
            _cpuCard.Subtitle = $"{s.CpuPercent:0.#}%  -  {s.ProcessCount} processes, {Fmt.Num(s.ThreadCount)} threads"
            _ramCard.Subtitle = $"{Fmt.Bytes(s.RamUsedBytes)} of {Fmt.Bytes(s.RamTotalBytes)} used"
            _cpuCard.Invalidate()
            _ramCard.Invalidate()
        End Sub

        Private Sub LoadSpecs()
            Dim m = HardwareInfo.Get_()
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine(m.ComputerName & "  -  " & m.UserName)
            sb.AppendLine(Fmt.Ellipsis(m.OsName, 30) & "  " & m.OsArchitecture)
            sb.AppendLine(Fmt.Ellipsis(m.CpuName, 42))
            sb.AppendLine($"{m.CpuCores}C / {m.CpuThreads}T  -  RAM " & Fmt.Bytes(m.RamTotalBytes))
            If m.GpuNames.Count > 0 Then sb.AppendLine("GPU " & Fmt.Ellipsis(m.GpuNames(0), 36))
            If m.BatteryPercent >= 0 Then
                sb.AppendLine($"Battery {m.BatteryPercent}%" & If(m.BatteryCharging, " (charging)", ""))
            End If
            sb.AppendLine("Up " & Fmt.Duration(TimeSpan.FromMilliseconds(Environment.TickCount64)))
            _specText.SetText(sb.ToString())
            _specCard.Subtitle = If(String.IsNullOrWhiteSpace(m.Manufacturer), "", Fmt.Ellipsis(m.Manufacturer & " " & m.Model, 34))

            _diskTable.SetRows(m.Disks.Select(
                Function(d) New CatRow(d, d.Name.TrimEnd("\"c) & " " & Fmt.Ellipsis(d.Label, 12) &
                                          "  " & d.UsedPercent.ToString("0") & "%",
                                       Fmt.Bytes(d.UsedBytes), Fmt.Bytes(d.FreeBytes)) With {
                    .IconName = "disk",
                    .Tone = If(d.UsedPercent > 90, ThemeManager.Danger,
                               If(d.UsedPercent > 75, ThemeManager.Warn, Color.Empty))}).ToList())
        End Sub

        Private Sub SwitchTab()
            Dim procs = _tabs.SelectedIndex = 0
            _procTable.Visible = procs
            _startupTable.Visible = Not procs
            _listCard.Title = If(procs, "Running processes", "Startup items")
            _btnA.Caption = If(procs, "End process", "Toggle enabled")
            _btnA.IconName = If(procs, "ban", "power")
            _btnA.Kind = If(procs, ButtonKind.Danger, ButtonKind.Secondary)
            _btnB.Caption = If(procs, "Scan image", "Scan file")
            _listCard.Invalidate()
        End Sub

        Private Sub RefreshLists()
            _btnRefresh.Busy = True
            Try
                _procTable.SetRows(ProcessInspector.Snapshot().Select(
                    Function(p) New CatRow(p, p.Name, p.Pid.ToString(), Fmt.Bytes(p.WorkingSetBytes),
                                           p.Threads.ToString(), p.TrustLabel,
                                           If(String.IsNullOrEmpty(p.Signer), p.Description, p.Signer)) With {
                        .IconName = TrustIcon(p),
                        .Tone = TrustColor(p)}).ToList())

                _startupTable.SetRows(StartupManager.All().Select(
                    Function(s) New CatRow(s, s.Name, s.Location, If(s.Enabled, "Enabled", "Disabled"),
                                           If(String.IsNullOrEmpty(s.Signer), "-", s.Signer), s.Command) With {
                        .IconName = If(s.Enabled, "power", "ban"),
                        .Tone = If(Not s.Enabled, ThemeManager.FgDim,
                                   If(s.Signed.HasValue AndAlso Not s.Signed.Value, ThemeManager.Warn, Color.Empty))}).ToList())
            Finally
                _btnRefresh.Busy = False
            End Try
        End Sub

        Private Shared Function TrustIcon(p As ProcessRow) As String
            If p.IsProtected Then Return "lock"
            If Not p.Signed.HasValue Then Return "info"
            Return If(p.Signed.Value, "check-circle", "alert")
        End Function

        Private Shared Function TrustColor(p As ProcessRow) As Color
            If p.IsProtected Then Return ThemeManager.FgDim
            If Not p.Signed.HasValue Then Return Color.Empty
            Return If(p.Signed.Value, ThemeManager.Ok, ThemeManager.Warn)
        End Function

        ' -- actions ----------------------------------------------------------

        Private Sub PrimaryAction()
            If _tabs.SelectedIndex = 0 Then
                Dim r = _procTable.SelectedRows.FirstOrDefault()
                If r Is Nothing Then
                    Say("Select a process first.", True)
                    Return
                End If
                Dim p = CType(r.Tag, ProcessRow)
                If Not Ask("End " & p.Name & "?",
                           "PID " & p.Pid & " and its child processes will be terminated. Unsaved work in that program is lost.",
                           "End process", True) Then Return
                Dim res = ProcessInspector.Kill(p.Pid)
                Say(res.Message, Not res.Ok)
                RefreshLists()
            Else
                Dim r = _startupTable.SelectedRows.FirstOrDefault()
                If r Is Nothing Then
                    Say("Select a startup item first.", True)
                    Return
                End If
                Dim s = CType(r.Tag, StartupEntry)
                Dim res = StartupManager.SetEnabled(s, Not s.Enabled)
                Say(res.Message, Not res.Ok)
                RefreshLists()
            End If
        End Sub

        Private Sub SecondaryAction()
            Dim path = SelectedPath()
            If String.IsNullOrEmpty(path) Then
                Say("No file path available for that item.", True)
                Return
            End If
            _btnB.Busy = True
            Try
                Dim det = ProcessInspector.ScanImage(path)
                If det Is Nothing Then
                    CatDialog.Alert(FindForm(), "Clean", path & Environment.NewLine & Environment.NewLine &
                                    "No signature, pattern or heuristic match.", DialogTone.Success)
                Else
                    Dim sb As New System.Text.StringBuilder()
                    sb.AppendLine(det.FilePath)
                    sb.AppendLine()
                    sb.AppendLine("Severity: " & SeverityUi.Label(det.Severity))
                    sb.AppendLine("Source:   " & det.Source.ToString())
                    For Each why In det.Reasons
                        sb.AppendLine("  - " & why)
                    Next
                    CatDialog.Alert(FindForm(), det.ThreatName, sb.ToString(), DialogTone.Danger)
                End If
            Finally
                _btnB.Busy = False
            End Try
        End Sub

        Private Sub TertiaryAction()
            Dim path = SelectedPath()
            If Not String.IsNullOrEmpty(path) Then ProcessInspector.OpenLocation(path)
        End Sub

        Private Function SelectedPath() As String
            If _tabs.SelectedIndex = 0 Then
                Dim r = _procTable.SelectedRows.FirstOrDefault()
                Return If(r Is Nothing, "", CType(r.Tag, ProcessRow).ImagePath)
            Else
                Dim r = _startupTable.SelectedRows.FirstOrDefault()
                Return If(r Is Nothing, "", CType(r.Tag, StartupEntry).ExecutablePath)
            End If
        End Function

        Public Overrides Sub Relayout()
            If _cpuCard Is Nothing Then Return
            Dim w = Math.Max(660, Width - Pad * 2)
            Dim q = (w - 3 * 14) \ 4

            _cpuCard.SetBounds(Pad, Pad, q, 176)
            _cpuSpark.SetBounds(12, _cpuCard.ContentTop - 6, q - 24, 176 - _cpuCard.ContentTop - 4)

            _ramCard.SetBounds(Pad + q + 14, Pad, q, 176)
            _ramSpark.SetBounds(12, _ramCard.ContentTop - 6, q - 24, 176 - _ramCard.ContentTop - 4)

            _specCard.SetBounds(Pad + (q + 14) * 2, Pad, q, 176)
            _specText.SetBounds(16, _specCard.ContentTop - 6, q - 32, 176 - _specCard.ContentTop)

            _diskCard.SetBounds(Pad + (q + 14) * 3, Pad, q, 176)
            _diskTable.SetBounds(8, _diskCard.ContentTop - 6, q - 16, 176 - _diskCard.ContentTop)

            _tabs.SetBounds(Pad, Pad + 192, 300, 38)

            Dim y = Pad + 240
            Dim h = Math.Max(240, Height - y - Pad)
            _listCard.SetBounds(Pad, y, w, h)
            Dim ct = _listCard.ContentTop
            _filter.SetBounds(w - 288, 12, 270, 34)
            _procTable.SetBounds(8, ct - 6, w - 16, h - ct - 48)
            _startupTable.SetBounds(8, ct - 6, w - 16, h - ct - 48)
            _btnA.SetBounds(12, h - 48, 152, 38)
            _btnB.SetBounds(172, h - 48, 142, 38)
            _btnC.SetBounds(322, h - 48, 150, 38)
            _btnRefresh.SetBounds(w - 130, h - 48, 118, 38)
        End Sub

    End Class

End Namespace
