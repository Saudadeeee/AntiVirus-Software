Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls
Imports AVAK.Views

Namespace Forms

    ''' <summary>
    ''' Borderless shell: sidebar navigation, custom title bar, tray integration and
    ''' a manual hit-test so the window can still be resized and snapped.
    ''' </summary>
    Public Class MainForm
        Inherits Form

        Private Const SideW As Integer = 214
        Private Const HeadH As Integer = 58
        Private Const GripSize As Integer = 6

        Private ReadOnly _nav As New List(Of CatNavItem)()
        Private ReadOnly _views As New Dictionary(Of String, ViewBase)(StringComparer.OrdinalIgnoreCase)
        Private _sidebar As Panel
        Private _header As Panel
        Private _host As Panel
        Private _titleLbl As CatLabel
        Private _subLbl As CatLabel
        Private _adminBadge As CatBadge
        Private _rtBadge As CatBadge
        Private _btnMin As CatIconButton
        Private _btnMax As CatIconButton
        Private _btnClose As CatIconButton
        Private _brand As Control

        Private _tray As NotifyIcon
        Private _trayMenu As ContextMenuStrip
        Private _current As String = ""
        Private _reallyExit As Boolean
        Private _handoff As Global.System.Windows.Forms.Timer

        ' -- lifecycle --------------------------------------------------------

        Public Sub New()
            AppSettings.Current.ApplyTheme()

            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            DoubleBuffered = True
            FormBorderStyle = FormBorderStyle.None
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(1120, 700)
            Size = New Size(1280, 820)
            BackColor = ThemeManager.Bg
            Text = "AVAK"
            KeyPreview = True

            Try
                Icon = Icons.ToIcon("shield-check", 32, ThemeManager.On_(ThemeManager.Accent), ThemeManager.Accent)
            Catch
            End Try

            BuildChrome()
            BuildViews()
            BuildTray()

            AddHandler ThemeManager.Changed, AddressOf OnThemeChanged
            AddHandler RealtimeMonitor.StateChanged, Sub() Ui_(Sub() RefreshBadges())
            AddHandler RealtimeMonitor.ThreatDetected, AddressOf OnRealtimeThreat
            AddHandler QuarantineManager.Changed, Sub() Ui_(Sub() RefreshBadges())

            _handoff = New Global.System.Windows.Forms.Timer() With {.Interval = 1500}
            AddHandler _handoff.Tick, AddressOf PollHandoff
            _handoff.Start()

            Navigate("dashboard")
            RefreshBadges()
            LocalProfile.Current.NoteLaunch()
        End Sub

        ''' <summary>
        ''' A second instance (started by the Explorer verb) cannot run alongside this
        ''' one, so it leaves the requested path in a drop file that we pick up here.
        ''' </summary>
        Private Sub PollHandoff(sender As Object, e As EventArgs)
            Try
                If Not IO.File.Exists(AppPaths.ScanRequestFile) Then Return
                Dim target = IO.File.ReadAllText(AppPaths.ScanRequestFile).Trim()
                IO.File.Delete(AppPaths.ScanRequestFile)
                If String.IsNullOrWhiteSpace(target) Then Return
                ShowFromTray()
                ScanPath(target)
            Catch ex As Exception
                Logger.Warn("Hand-off failed: " & ex.Message)
            End Try
        End Sub

        ''' <summary>
        ''' Every registered page, in sidebar order. --uitest walks this so a new
        ''' page is covered by the self-test the moment it is registered.
        ''' </summary>
        Public ReadOnly Property ViewKeys As List(Of String)
            Get
                Return _nav.Select(Function(n) n.Key).Where(Function(k) _views.ContainsKey(k)).ToList()
            End Get
        End Property

        Public Sub ScanPath(target As String)
            Navigate("scan")
            CType(_views("scan"), ScanView).ScanSpecific(target)
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            ApplyRegion()
            If LocalProfile.Current.LockOnStart AndAlso LocalProfile.Current.HasPin Then RequestUnlock()
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If Not _reallyExit AndAlso e.CloseReason = CloseReason.UserClosing AndAlso
               AppSettings.Current.CloseToTray AndAlso _tray IsNot Nothing Then
                e.Cancel = True
                HideToTray()
                Return
            End If
            MyBase.OnFormClosing(e)
        End Sub

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            RemoveHandler ThemeManager.Changed, AddressOf OnThemeChanged
            Try
                _handoff?.Stop()
                _handoff?.Dispose()
                If _tray IsNot Nothing Then
                    _tray.Visible = False
                    _tray.Dispose()
                End If
            Catch
            End Try
            MyBase.OnFormClosed(e)
        End Sub

        ' -- chrome -----------------------------------------------------------

        Private Sub BuildChrome()
            _sidebar = New Panel With {.BackColor = ThemeManager.BgSide, .Width = SideW, .Dock = DockStyle.Left}
            Controls.Add(_sidebar)

            _brand = New BrandHeader() With {.Height = 96, .Dock = DockStyle.Top}
            AddHandler _brand.MouseDown, AddressOf DragHandler
            _sidebar.Controls.Add(_brand)

            Dim entries = {
                ("dashboard", "Dashboard", "grid"),
                ("scan", "Scan centre", "scan"),
                ("protection", "Protection", "shield-check"),
                ("quarantine", "Quarantine", "lock"),
                ("privacy", "Privacy", "sparkle"),
                ("network", "Network", "globe"),
                ("system", "System", "cpu"),
                ("rules", "Rules", "list"),
                ("history", "History", "history")}

            Dim y = 108
            For Each entry In entries
                Dim item As New CatNavItem(entry.Item1, entry.Item2, entry.Item3)
                item.SetBounds(6, y, SideW - 12, 44)
                Dim capturedKey = entry.Item1
                AddHandler item.Click, Sub() Navigate(capturedKey)
                _sidebar.Controls.Add(item)
                item.BringToFront()
                _nav.Add(item)
                y += 48
            Next

            Dim bottom = {("settings", "Settings", "settings"), ("account", "Account", "user")}
            Dim by = 0
            For Each entry In bottom
                Dim item As New CatNavItem(entry.Item1, entry.Item2, entry.Item3)
                Dim capturedKey = entry.Item1
                AddHandler item.Click, Sub() Navigate(capturedKey)
                item.Tag = "bottom:" & by
                _sidebar.Controls.Add(item)
                item.BringToFront()
                _nav.Add(item)
                by += 1
            Next

            _header = New Panel With {.BackColor = ThemeManager.Bg, .Height = HeadH, .Dock = DockStyle.Top}
            AddHandler _header.MouseDown, AddressOf DragHandler
            AddHandler _header.DoubleClick, Sub() ToggleMaximize()
            AddHandler _header.Paint, Sub(s As Object, e As PaintEventArgs)
                                          Using p As New Pen(ThemeManager.Border)
                                              e.Graphics.DrawLine(p, 0, HeadH - 1, _header.Width, HeadH - 1)
                                          End Using
                                      End Sub

            _titleLbl = New CatLabel With {.FontRole = "title", .Text_ = "Dashboard"}
            AddHandler _titleLbl.MouseDown, AddressOf DragHandler
            _header.Controls.Add(_titleLbl)

            _subLbl = New CatLabel With {.FontRole = "small", .Tone = ThemeManager.FgMuted}
            AddHandler _subLbl.MouseDown, AddressOf DragHandler
            _header.Controls.Add(_subLbl)

            _rtBadge = New CatBadge()
            _header.Controls.Add(_rtBadge)

            _adminBadge = New CatBadge()
            AddHandler _adminBadge.Click, Sub()
                                              If Elevation.IsAdmin Then Return
                                              If CatDialog.Confirm(Me, "Restart as administrator?",
                                                  "Firewall changes, machine-wide startup entries and protected files need elevation.",
                                                  "Restart") Then
                                                  If Elevation.Relaunch() Then
                                                      _reallyExit = True
                                                      Application.Exit()
                                                  End If
                                              End If
                                          End Sub
            _header.Controls.Add(_adminBadge)

            _btnMin = New CatIconButton("minus", "Minimise")
            AddHandler _btnMin.Click, Sub()
                                          If AppSettings.Current.MinimizeToTray Then
                                              HideToTray()
                                          Else
                                              WindowState = FormWindowState.Minimized
                                          End If
                                      End Sub
            _header.Controls.Add(_btnMin)

            _btnMax = New CatIconButton("maximize", "Maximise")
            AddHandler _btnMax.Click, Sub() ToggleMaximize()
            _header.Controls.Add(_btnMax)

            _btnClose = New CatIconButton("x", "Close") With {.HoverTone = ThemeManager.Danger}
            AddHandler _btnClose.Click, Sub() Close()
            _header.Controls.Add(_btnClose)

            _host = New Panel With {.Dock = DockStyle.Fill, .BackColor = ThemeManager.Bg}
            Controls.Add(_host)

            Controls.Add(_header)

            ' WinForms docks children from the HIGHEST child index down, so the
            ' Fill panel must sit at index 0 (docked last) and the outermost edges
            ' at the highest indices. Set them explicitly instead of guessing.
            Controls.SetChildIndex(_host, 0)
            Controls.SetChildIndex(_header, 1)
            Controls.SetChildIndex(_sidebar, 2)
        End Sub

        Private Sub BuildViews()
            Dim dash As New DashboardView()
            AddHandler dash.RequestNavigate, Sub(k As String) Navigate(k)
            AddHandler dash.RequestQuickScan, Sub()
                                                  Navigate("scan")
                                                  CType(_views("scan"), ScanView).RunQuickScan()
                                              End Sub
            Register("dashboard", dash)

            Dim scan As New ScanView()
            AddHandler scan.ScanCompleted, Sub(r As ScanReport) Ui_(Sub() RefreshBadges())
            Register("scan", scan)

            Register("protection", New ProtectionView())
            Register("quarantine", New QuarantineView())
            Register("privacy", New PrivacyView())
            Register("network", New NetworkView())
            Register("system", New SystemView())
            Register("rules", New RulesView())
            Register("history", New HistoryView())

            Dim settings As New SettingsView()
            AddHandler settings.ThemeApplied, Sub() OnThemeChanged(Nothing, EventArgs.Empty)
            Register("settings", settings)

            Dim account As New AccountView()
            AddHandler account.LockRequested, Sub() RequestUnlock()
            Register("account", account)
        End Sub

        Private Sub Register(key As String, view As ViewBase)
            view.Visible = False
            _views(key) = view
            _host.Controls.Add(view)
        End Sub

        Private Sub BuildTray()
            _trayMenu = New ContextMenuStrip()
            _trayMenu.Items.Add("Open AVAK", Nothing, Sub() ShowFromTray())
            _trayMenu.Items.Add(New ToolStripSeparator())
            _trayMenu.Items.Add("Quick scan", Nothing, Sub()
                                                           ShowFromTray()
                                                           Navigate("scan")
                                                           CType(_views("scan"), ScanView).RunQuickScan()
                                                       End Sub)
            _trayMenu.Items.Add("Toggle real-time protection", Nothing,
                Sub()
                    If RealtimeMonitor.IsRunning Then
                        RealtimeMonitor.Stop()
                        AppSettings.Current.RealtimeProtection = False
                    Else
                        RealtimeMonitor.Start()
                        AppSettings.Current.RealtimeProtection = True
                    End If
                    AppSettings.Current.Save()
                    RefreshBadges()
                End Sub)
            _trayMenu.Items.Add(New ToolStripSeparator())
            _trayMenu.Items.Add("Exit", Nothing, Sub()
                                                     _reallyExit = True
                                                     Application.Exit()
                                                 End Sub)

            _tray = New NotifyIcon() With {
                .Text = "AVAK",
                .Visible = True,
                .ContextMenuStrip = _trayMenu
            }
            Try
                _tray.Icon = Icons.ToIcon("shield-check", 32, ThemeManager.On_(ThemeManager.Accent), ThemeManager.Accent)
            Catch
                _tray.Icon = SystemIcons.Shield
            End Try
            AddHandler _tray.DoubleClick, Sub() ShowFromTray()
        End Sub

        ' -- navigation -------------------------------------------------------

        Public Sub Navigate(key As String)
            If Not _views.ContainsKey(key) Then Return
            If _current = key Then Return

            If _current <> "" AndAlso _views.ContainsKey(_current) Then
                _views(_current).OnDeactivated()
                _views(_current).Visible = False
            End If

            _current = key
            Dim v = _views(key)
            v.EnsureBuilt()
            v.Visible = True
            v.BringToFront()
            v.OnActivated()

            _titleLbl.SetText(v.Title)
            _subLbl.SetText(v.Subtitle)

            For Each n In _nav
                n.Active = String.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase)
            Next
            LayoutHeader()
        End Sub

        Private Sub RefreshBadges()
            If IsDisposed Then Return
            Dim rt = RealtimeMonitor.IsRunning
            _rtBadge.SetContent(If(rt, "Shield on", "Shield off"),
                                If(rt, ThemeManager.Ok, ThemeManager.Warn),
                                If(rt, "shield-check", "shield-off"))
            _adminBadge.SetContent(If(Elevation.IsAdmin, "Administrator", "Standard user"),
                                   If(Elevation.IsAdmin, ThemeManager.Colors.Sapphire, ThemeManager.FgMuted),
                                   If(Elevation.IsAdmin, "key", "user"))
            _adminBadge.Cursor = If(Elevation.IsAdmin, Cursors.Default, Cursors.Hand)

            Dim q = _nav.FirstOrDefault(Function(n) n.Key = "quarantine")
            If q IsNot Nothing Then
                q.BadgeCount = QuarantineManager.Count
                q.Invalidate()
            End If

            If _tray IsNot Nothing Then
                _tray.Text = "AVAK - " & If(rt, "protected", "shield off")
            End If
            LayoutHeader()
        End Sub

        Private Sub OnRealtimeThreat(sender As Object, d As Detection)
            Ui_(Sub()
                    RefreshBadges()
                    Toast.Danger("Threat blocked", d.ThreatName & Environment.NewLine & Fmt.ShortPath(d.FilePath, 46))
                    If AppSettings.Current.OnThreatFound = ThreatAction.AskMe Then
                        If CatDialog.Confirm(Me, "Threat detected",
                                             d.ThreatName & Environment.NewLine & Environment.NewLine & d.FilePath &
                                             Environment.NewLine & Environment.NewLine & d.ReasonText,
                                             "Quarantine it", DialogTone.Danger, True) Then
                            Dim r = QuarantineManager.Quarantine(d)
                            Toast.Notify("Quarantine", r.Message,
                                         If(r.Ok, ThemeManager.Ok, ThemeManager.Danger),
                                         If(r.Ok, "lock", "alert"))
                            RefreshBadges()
                        End If
                    End If
                End Sub)
        End Sub

        ' -- tray -------------------------------------------------------------

        Private Sub HideToTray()
            Hide()
            ShowInTaskbar = False
            Try
                _tray.ShowBalloonTip(2500, "AVAK is still running",
                                     If(RealtimeMonitor.IsRunning,
                                        "Real-time protection stays active.",
                                        "Right-click the tray icon to reopen."),
                                     ToolTipIcon.Info)
            Catch
            End Try
        End Sub

        Private Sub ShowFromTray()
            ShowInTaskbar = True
            Show()
            WindowState = FormWindowState.Normal
            Activate()
            BringToFront()
        End Sub

        ' -- lock -------------------------------------------------------------

        Private Sub RequestUnlock()
            Dim p = LocalProfile.Current
            If Not p.HasPin Then Return
            While True
                Dim pin = CatDialog.Prompt(Me, "AVAK is locked",
                                           "Enter your PIN to continue. Real-time protection keeps running while locked.",
                                           "", "PIN", True)
                If pin Is Nothing Then
                    HideToTray()
                    Return
                End If
                If p.VerifyPin(pin) Then Return
                CatDialog.Alert(Me, "Wrong PIN", "That PIN is not correct.", DialogTone.Warning)
            End While
        End Sub

        ' -- window behaviour -------------------------------------------------

        Private Sub DragHandler(sender As Object, e As MouseEventArgs)
            If e.Button = MouseButtons.Left AndAlso WindowState <> FormWindowState.Maximized Then
                NativeMethods.DragWindow(Me)
            End If
        End Sub

        Private Sub ToggleMaximize()
            WindowState = If(WindowState = FormWindowState.Maximized, FormWindowState.Normal, FormWindowState.Maximized)
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            LayoutSidebar()
            LayoutHeader()
            ApplyRegion()
            If _btnMax IsNot Nothing Then
                _btnMax.IconName = If(WindowState = FormWindowState.Maximized, "minus", "maximize")
            End If
        End Sub

        Private Sub ApplyRegion()
            Try
                If WindowState = FormWindowState.Maximized Then
                    Region = Nothing
                Else
                    Region = New Region(Gfx.RoundedPath(New RectangleF(0, 0, Width, Height), 14))
                End If
            Catch
            End Try
        End Sub

        Private Sub LayoutSidebar()
            If _sidebar Is Nothing Then Return
            Dim bottomItems = _nav.Where(Function(n) TypeOf n.Tag Is String AndAlso CStr(n.Tag).StartsWith("bottom:")).ToList()
            Dim y = _sidebar.Height - 12 - bottomItems.Count * 48
            For Each item In bottomItems
                item.SetBounds(6, y, SideW - 12, 44)
                y += 48
            Next
        End Sub

        Private Sub LayoutHeader()
            If _header Is Nothing Then Return
            Dim w = _header.Width
            _titleLbl.SetBounds(24, 8, 420, 26)
            _subLbl.SetBounds(24, 32, 480, 18)

            Dim x = w - 12 - 34
            _btnClose.SetBounds(x, 12, 34, 34)
            x -= 38
            _btnMax.SetBounds(x, 12, 34, 34)
            x -= 38
            _btnMin.SetBounds(x, 12, 34, 34)

            x -= 14
            _adminBadge.AutoWidth()
            x -= _adminBadge.Width
            _adminBadge.SetBounds(x, 17, _adminBadge.Width, 24)

            x -= 10
            _rtBadge.AutoWidth()
            x -= _rtBadge.Width
            _rtBadge.SetBounds(x, 17, _rtBadge.Width, 24)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If WindowState <> FormWindowState.Maximized Then
                Gfx.DrawRounded(e.Graphics, New RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 14,
                                ThemeManager.Border, 1.2F)
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            If IsDisposed Then Return
            BackColor = ThemeManager.Bg
            _sidebar.BackColor = ThemeManager.BgSide
            _header.BackColor = ThemeManager.Bg
            _host.BackColor = ThemeManager.Bg
            _subLbl.Tone = ThemeManager.FgMuted
            _btnClose.HoverTone = ThemeManager.Danger
            Try
                Dim ic = Icons.ToIcon("shield-check", 32, ThemeManager.On_(ThemeManager.Accent), ThemeManager.Accent)
                Icon = ic
                If _tray IsNot Nothing Then _tray.Icon = ic
            Catch
            End Try
            RefreshBadges()
            Invalidate(True)
        End Sub

        ''' <summary>Manual hit-testing so a borderless window still resizes from the edges.</summary>
        Protected Overrides Sub WndProc(ByRef m As Message)
            Const WM_NCHITTEST As Integer = &H84
            If m.Msg = WM_NCHITTEST AndAlso WindowState = FormWindowState.Normal Then
                MyBase.WndProc(m)
                Dim pos = PointToClient(New Point(m.LParam.ToInt32() And &HFFFF,
                                                  (m.LParam.ToInt32() >> 16) And &HFFFF))
                Dim onLeft = pos.X <= GripSize
                Dim onRight = pos.X >= Width - GripSize
                Dim onTop = pos.Y <= GripSize
                Dim onBottom = pos.Y >= Height - GripSize

                If onTop AndAlso onLeft Then m.Result = New IntPtr(13)
                If onTop AndAlso onRight Then m.Result = New IntPtr(14)
                If onBottom AndAlso onLeft Then m.Result = New IntPtr(16)
                If onBottom AndAlso onRight Then m.Result = New IntPtr(17)
                If m.Result = IntPtr.Zero OrElse m.Result.ToInt32() = 1 Then
                    If onLeft Then m.Result = New IntPtr(10)
                    If onRight Then m.Result = New IntPtr(11)
                    If onTop Then m.Result = New IntPtr(12)
                    If onBottom Then m.Result = New IntPtr(15)
                End If
                Return
            End If
            MyBase.WndProc(m)
        End Sub

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.Control Then
                Select Case e.KeyCode
                    Case Keys.D1 : Navigate("dashboard")
                    Case Keys.D2 : Navigate("scan")
                    Case Keys.D3 : Navigate("protection")
                    Case Keys.D4 : Navigate("quarantine")
                    Case Keys.D5 : Navigate("privacy")
                    Case Keys.D6 : Navigate("network")
                    Case Keys.D7 : Navigate("system")
                    Case Keys.D8 : Navigate("rules")
                    Case Keys.D9 : Navigate("history")
                    Case Keys.OemComma : Navigate("settings")
                    Case Keys.L
                        If LocalProfile.Current.HasPin Then RequestUnlock()
                End Select
            End If
        End Sub

        Private Sub Ui_(action As Action)
            If IsDisposed OrElse Not IsHandleCreated Then Return
            Try
                If InvokeRequired Then BeginInvoke(action) Else action()
            Catch
            End Try
        End Sub

    End Class

    ''' <summary>Sidebar logo block.</summary>
    Friend Class BrandHeader
        Inherits CatControlBase

        Public Sub New()
            BackColor = Color.Transparent
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim accent = ThemeManager.Accent

            Dim box As New RectangleF(20, 22, 48, 48)
            Gfx.GradientRounded(g, box, 14, ThemeManager.Mix(accent, Color.White, 0.24F), accent)
            Icons.Draw(g, "shield-check", RectangleF.Inflate(box, -12, -12), ThemeManager.On_(accent), 2.2F)

            Gfx.TextIn(g, "AVAK", ThemeManager.GetFont(18.0F, FontStyle.Bold), ThemeManager.Fg,
                       New RectangleF(80, 24, Width - 90, 26))
            Gfx.TextIn(g, "Security suite", ThemeManager.Small, ThemeManager.FgDim,
                       New RectangleF(80, 48, Width - 90, 18))

            Using p As New Pen(ThemeManager.Border)
                g.DrawLine(p, 16, Height - 6, Width - 16, Height - 6)
            End Using
        End Sub
    End Class

End Namespace
