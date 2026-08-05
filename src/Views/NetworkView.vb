Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Network
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Firewall control, live TCP connections and Windows VPN profiles.</summary>
    Public Class NetworkView
        Inherits ViewBase

        Private _fwCard As CatCard
        Private _fwRows As CatListRow()
        Private _fwAllOn As CatButton
        Private _fwAllOff As CatButton
        Private _fwReset As CatButton
        Private _fwHint As CatLabel
        Private _fwClearRules As CatButton
        Private _adapterTable As CatTable

        Private _vpnCard As CatCard
        Private _vpnTable As CatTable
        Private _vpnConnect As CatButton
        Private _vpnDisconnect As CatButton
        Private _vpnCreate As CatButton
        Private _vpnDelete As CatButton
        Private _vpnSettings As CatButton

        Private _connCard As CatCard
        Private _connTable As CatTable
        Private _connFilter As CatInput
        Private _externalOnly As CatToggle
        Private _connRefresh As CatButton
        Private _blockApp As CatButton

        Private _connTabs As CatSegment
        Private _timer As Global.System.Windows.Forms.Timer
        Private _allConnections As New List(Of ConnectionRow)()

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Network"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Firewall, live connections and VPN profiles"
            End Get
        End Property

        Protected Overrides Sub Build()
            ' -- firewall
            _fwCard = Card("Windows Firewall", "shield")
            _fwRows = {New CatListRow(), New CatListRow(), New CatListRow()}
            For Each r In _fwRows
                _fwCard.Controls.Add(r)
            Next
            _fwAllOn = Btn("Turn all on", ButtonKind.Success, "shield-check",
                           Sub() SetFirewall(True), _fwCard)
            _fwAllOff = Btn("Turn all off", ButtonKind.Danger, "shield-off",
                            Sub() SetFirewall(False), _fwCard)
            _fwReset = Btn("Reset", ButtonKind.Ghost, "rotate",
                           Sub()
                               If Ask("Reset firewall rules?",
                                      "This runs 'netsh advfirewall reset' and restores the Windows defaults. Custom rules you added will be lost.",
                                      "Reset", True) Then
                                   Dim r = FirewallManager.RestoreDefaults()
                                   Say(r.Message, Not r.Ok)
                                   RefreshFirewall()
                               End If
                           End Sub, _fwCard)
            _fwHint = Lbl("", "small", ThemeManager.FgMuted, _fwCard)
            _fwClearRules = Btn("Clear rules", ButtonKind.Ghost, "trash",
                                Sub()
                                    If Not Ask("Remove every AVAK firewall rule?",
                                               "All rules AVAK created (named 'AVAK block *') will be deleted. " &
                                               "Rules you or Windows created are untouched.",
                                               "Remove", True) Then Return
                                    Dim r = FirewallManager.RemoveAvakRules()
                                    Say(r.Message, Not r.Ok)
                                    RefreshFirewall()
                                End Sub, _fwCard)


            ' -- vpn
            _vpnCard = Card("VPN profiles", "globe")
            _vpnCard.Subtitle = "AVAK dials Windows VPN profiles"
            _vpnTable = New CatTable With {
                .RowHeight = 36, .EmptyText = "No VPN profiles configured in Windows", .EmptyIcon = "globe"}
            _vpnTable.SetColumns(New CatColumn("Name", 0),
                                 New CatColumn("Server", 190, StringAlignment.Near, True),
                                 New CatColumn("Type", 96, StringAlignment.Near, True),
                                 New CatColumn("Status", 110))
            _vpnCard.Controls.Add(_vpnTable)
            _vpnConnect = Btn("Connect", ButtonKind.Primary, "power", Sub() VpnConnect(), _vpnCard)
            _vpnDisconnect = Btn("Disconnect", ButtonKind.Secondary, "ban", Sub() VpnDisconnect(), _vpnCard)
            _vpnCreate = Btn("New profile", ButtonKind.Ghost, "plus", Sub() VpnCreate(), _vpnCard)
            _vpnDelete = Btn("Delete", ButtonKind.Ghost, "trash", Sub() VpnDelete(), _vpnCard)
            _vpnSettings = Btn("Windows VPN settings", ButtonKind.Ghost, "link",
                               Sub() VpnManager.OpenWindowsVpnSettings(), _vpnCard)

            ' -- connections
            _connCard = Card("Active connections", "activity")
            _connCard.ReserveSubtitle = True
            _connFilter = New CatInput With {.Placeholder = "Filter by process, address or port...", .IconName = "search"}
            AddHandler _connFilter.TextChangedEx, Sub() _connTable.Filter = _connFilter.Value
            _connCard.Controls.Add(_connFilter)

            _externalOnly = New CatToggle("External only", "", False) With {.TrailingSwitch = False}
            AddHandler _externalOnly.CheckedChanged, Sub() ShowConnections()
            _connCard.Controls.Add(_externalOnly)

            _connTable = New CatTable With {
                .RowHeight = 34, .EmptyText = "No TCP connections", .EmptyIcon = "globe"}
            _connTable.SetColumns(New CatColumn("Process", 190),
                                  New CatColumn("PID", 66, StringAlignment.Far, True),
                                  New CatColumn("Local", 168, StringAlignment.Near, True),
                                  New CatColumn("Remote", 0),
                                  New CatColumn("State", 110, StringAlignment.Near, True))
            _connCard.Controls.Add(_connTable)

            _adapterTable = New CatTable With {
                .RowHeight = 34, .EmptyText = "No network adapters", .EmptyIcon = "network"}
            _adapterTable.SetColumns(New CatColumn("Adapter", 0),
                                     New CatColumn("Type", 118, StringAlignment.Near, True),
                                     New CatColumn("IPv4", 132),
                                     New CatColumn("Gateway", 132, StringAlignment.Near, True),
                                     New CatColumn("DNS", 150, StringAlignment.Near, True),
                                     New CatColumn("Link", 84, StringAlignment.Far, True),
                                     New CatColumn("Traffic", 150, StringAlignment.Far, True))
            _connCard.Controls.Add(_adapterTable)

            _connTabs = New CatSegment()
            _connTabs.SetItems("Connections", "Adapters")
            AddHandler _connTabs.SelectionChanged, Sub()
                                                       SwitchConnTab()
                                                       Relayout()
                                                   End Sub
            _connCard.Controls.Add(_connTabs)

            _connRefresh = Btn("Refresh", ButtonKind.Secondary, "refresh",
                               Sub()
                                   RefreshConnections()
                                   RefreshAdapters()
                               End Sub, _connCard)
            _blockApp = Btn("Block program in firewall", ButtonKind.Danger, "ban", Sub() BlockSelected(), _connCard)

            _timer = New Global.System.Windows.Forms.Timer() With {.Interval = 6000}
            AddHandler _timer.Tick, Sub() RefreshConnections()
        End Sub

        Public Overrides Sub OnActivated()
            RefreshFirewall()
            RefreshVpn()
            RefreshConnections()
            RefreshAdapters()
            SwitchConnTab()
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

        ' -- firewall ---------------------------------------------------------

        Private Sub RefreshFirewall()
            Dim st = FirewallManager.Read()
            If Not st.Available Then
                SetRow(_fwRows(0), False, "Firewall state", If(String.IsNullOrEmpty(st.Error_), "Unavailable", st.Error_), "?")
                SetRow(_fwRows(1), True, "", "", "")
                SetRow(_fwRows(2), True, "", "", "")
                _fwCard.Accent = ThemeManager.Warn
                _fwCard.Invalidate()
                Return
            End If
            SetRow(_fwRows(0), st.DomainEnabled, "Domain profile", "Networks joined to a domain",
                   If(st.DomainEnabled, "On", "Off"))
            SetRow(_fwRows(1), st.PrivateEnabled, "Private profile", "Home and work networks",
                   If(st.PrivateEnabled, "On", "Off"))
            SetRow(_fwRows(2), st.PublicEnabled, "Public profile", "Cafes, airports, untrusted Wi-Fi",
                   If(st.PublicEnabled, "On", "Off"))
            _fwCard.Accent = If(st.AllOn, ThemeManager.Ok, ThemeManager.Danger)
            _fwCard.Invalidate()
            _fwHint.SetText(st.Summary & "   -   " & FirewallManager.CountAvakRules() & " AVAK block rule(s)")
        End Sub

        Private Sub SetFirewall(enable As Boolean)
            If Not enable Then
                If Not Ask("Turn the firewall off?",
                           "Disabling the firewall exposes every listening service on this machine to the network. " &
                           "Only do this temporarily and turn it back on as soon as you are done.",
                           "Turn off", True) Then Return
            End If
            Dim r = FirewallManager.SetAll(enable)
            Say(r.Message, Not r.Ok)
            RefreshFirewall()
        End Sub

        Private Shared Sub SetRow(r As CatListRow, ok As Boolean, title As String, detail As String, trailing As String)
            r.IconName = If(ok, "check-circle", "shield-off")
            r.Tone = If(ok, ThemeManager.Ok, ThemeManager.Danger)
            r.Title = title
            r.Description = detail
            r.TrailingText = trailing
            r.Visible = Not String.IsNullOrEmpty(title)
            r.Invalidate()
        End Sub

        ' -- vpn --------------------------------------------------------------

        Private Sub RefreshVpn()
            Dim entries = VpnManager.List()
            _vpnTable.SetRows(entries.Select(
                Function(v) New CatRow(v, v.Name, If(String.IsNullOrEmpty(v.ServerAddress), "-", v.ServerAddress),
                                       If(String.IsNullOrEmpty(v.TunnelType), "-", v.TunnelType),
                                       If(v.Connected, "Connected", "Disconnected")) With {
                    .IconName = If(v.Connected, "check-circle", "globe"),
                    .Tone = If(v.Connected, ThemeManager.Ok, Color.Empty)}).ToList())
            _vpnCard.Accent = If(VpnManager.IsAnyConnected, ThemeManager.Ok, Color.Empty)
            _vpnCard.Invalidate()
        End Sub

        Private Function SelectedVpn() As VpnEntry
            Dim r = _vpnTable.SelectedRows.FirstOrDefault()
            Return If(r Is Nothing, Nothing, CType(r.Tag, VpnEntry))
        End Function

        Private Sub VpnConnect()
            Dim v = SelectedVpn()
            If v Is Nothing Then
                Say("Pick a VPN profile first.", True)
                Return
            End If
            Dim user = CatDialog.Prompt(FindForm(), "Connect to " & v.Name,
                                        "User name (leave empty to use the saved credentials).", "", "user name")
            If user Is Nothing Then Return
            Dim pass As String = ""
            If Not String.IsNullOrWhiteSpace(user) Then
                pass = CatDialog.Prompt(FindForm(), "Connect to " & v.Name, "Password", "", "password", True)
                If pass Is Nothing Then Return
            End If

            _vpnConnect.Busy = True
            Try
                Dim r = VpnManager.Connect(v.Name, user, pass)
                Say(r.Message, Not r.Ok)
            Finally
                _vpnConnect.Busy = False
            End Try
            RefreshVpn()
        End Sub

        Private Sub VpnDisconnect()
            Dim v = SelectedVpn()
            If v Is Nothing Then Return
            Dim r = VpnManager.Disconnect(v.Name)
            Say(r.Message, Not r.Ok)
            RefreshVpn()
        End Sub

        Private Sub VpnCreate()
            Dim name = CatDialog.Prompt(FindForm(), "New VPN profile", "A name for this connection.", "", "Office VPN")
            If String.IsNullOrWhiteSpace(name) Then Return
            Dim server = CatDialog.Prompt(FindForm(), "New VPN profile", "Server host name or IP address.", "", "vpn.example.com")
            If String.IsNullOrWhiteSpace(server) Then Return
            Dim r = VpnManager.CreateProfile(name, server, "Automatic")
            Say(r.Message, Not r.Ok)
            RefreshVpn()
        End Sub

        Private Sub VpnDelete()
            Dim v = SelectedVpn()
            If v Is Nothing Then Return
            If Not Ask("Delete '" & v.Name & "'?", "The Windows VPN profile will be removed.", "Delete", True) Then Return
            Dim r = VpnManager.DeleteProfile(v.Name)
            Say(r.Message, Not r.Ok)
            RefreshVpn()
        End Sub

        ' -- connections ------------------------------------------------------

        Private Sub RefreshConnections()
            If Not Visible Then Return
            _allConnections = ConnectionMonitor.Connections()
            ShowConnections()
        End Sub

        Private Sub ShowConnections()
            Dim src = If(_externalOnly.Checked, _allConnections.Where(Function(c) c.IsExternal).ToList(), _allConnections)
            _connTable.SetRows(src.Select(
                Function(c) New CatRow(c, If(String.IsNullOrEmpty(c.ProcessName), "(pid " & c.Pid & ")", c.ProcessName),
                                       c.Pid.ToString(), c.Local, c.Remote, c.State) With {
                    .IconName = If(c.IsExternal, "globe", If(c.IsListening, "server", "network")),
                    .Tone = If(c.IsExternal, ThemeManager.Colors.Sapphire, Color.Empty)}).ToList())
            _connCard.Subtitle = $"{src.Count} connection(s), {_allConnections.Where(Function(c) c.IsExternal).Count()} to external hosts"
            _connCard.Invalidate()
        End Sub

        Private Sub SwitchConnTab()
            Dim conns = _connTabs.SelectedIndex = 0
            _connTable.Visible = conns
            _adapterTable.Visible = Not conns
            _externalOnly.Visible = conns
            _connFilter.Visible = conns
            _blockApp.Visible = conns
        End Sub

        Private Sub RefreshAdapters()
            _adapterTable.SetRows(ConnectionMonitor.Adapters().Select(
                Function(a) New CatRow(a, a.Name,
                                       a.Kind,
                                       If(a.Addresses.Count = 0, "-", String.Join(", ", a.Addresses)),
                                       If(a.Gateways.Count = 0, "-", a.Gateways(0)),
                                       If(a.DnsServers.Count = 0, "-", String.Join(", ", a.DnsServers.Take(2))),
                                       If(a.SpeedMbps > 0, a.SpeedMbps & " Mb", "-"),
                                       Fmt.Bytes(a.BytesReceived) & " in / " & Fmt.Bytes(a.BytesSent) & " out") With {
                    .IconName = AdapterIcon(a),
                    .Tone = If(a.Status = "Up", ThemeManager.Ok, ThemeManager.FgDim)}).ToList())
        End Sub

        Private Shared Function AdapterIcon(a As AdapterRow) As String
            If a.Kind.IndexOf("Wireless", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "wifi"
            If a.Kind.IndexOf("Tunnel", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               a.Kind.IndexOf("Ppp", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "lock"
            Return "network"
        End Function

        Private Sub BlockSelected()
            Dim r = _connTable.SelectedRows.FirstOrDefault()
            If r Is Nothing Then
                Say("Select a connection first.", True)
                Return
            End If
            Dim c = CType(r.Tag, ConnectionRow)
            If String.IsNullOrEmpty(c.ProcessPath) Then
                Say("AVAK cannot see the program's path (it may be a protected process).", True)
                Return
            End If
            If Not Ask("Block " & c.ProcessName & "?",
                       "Two Windows Firewall rules will be created to block inbound and outbound traffic for:" &
                       Environment.NewLine & Environment.NewLine & c.ProcessPath,
                       "Block", True) Then Return
            Dim res = FirewallManager.BlockProgram(c.ProcessPath, c.ProcessName)
            Say(res.Message, Not res.Ok)
            RefreshFirewall()
        End Sub

        Public Overrides Sub Relayout()
            If _fwCard Is Nothing Then Return
            Dim w = Math.Max(640, Width - Pad * 2)
            Dim colW = (w - 20) \ 2

            _fwCard.SetBounds(Pad, Pad, colW, 268)
            Dim t1 = _fwCard.ContentTop
            For i = 0 To _fwRows.Length - 1
                _fwRows(i).SetBounds(8, t1 - 8 + i * 50, colW - 16, 48)
            Next
            _fwHint.SetBounds(16, t1 + 146, colW - 32, 18)
            _fwAllOn.SetBounds(14, 268 - 50, 122, 38)
            _fwAllOff.SetBounds(142, 268 - 50, 122, 38)
            _fwReset.SetBounds(270, 268 - 50, 100, 38)
            _fwClearRules.SetBounds(376, 268 - 50, 112, 38)

            _vpnCard.SetBounds(Pad + colW + 20, Pad, colW, 268)
            Dim t2 = _vpnCard.ContentTop
            _vpnTable.SetBounds(8, t2 - 6, colW - 16, 268 - t2 - 50)
            _vpnConnect.SetBounds(12, 268 - 46, 112, 36)
            _vpnDisconnect.SetBounds(130, 268 - 46, 122, 36)
            _vpnCreate.SetBounds(258, 268 - 46, 128, 36)
            _vpnDelete.SetBounds(392, 268 - 46, 104, 36)
            _vpnSettings.SetBounds(colW - 210, 12, 198, 32)

            Dim y = Pad + 284
            Dim h = Math.Max(240, Height - y - Pad)
            _connCard.SetBounds(Pad, y, w, h)
            Dim t3 = _connCard.ContentTop
            _connFilter.SetBounds(w - 320, 12, 302, 34)
            _connTabs.SetBounds(12, t3 - 10, 260, 34)
            _externalOnly.SetBounds(286, t3 - 10, 170, 34)
            Dim listRect = New Rectangle(8, t3 + 32, w - 16, h - t3 - 90)
            _connTable.Bounds = listRect
            _adapterTable.Bounds = listRect
            _connRefresh.SetBounds(12, h - 50, 122, 38)
            _blockApp.SetBounds(142, h - 50, 232, 38)
        End Sub

    End Class

End Namespace
