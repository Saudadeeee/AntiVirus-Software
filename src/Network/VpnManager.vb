Imports System.Net.NetworkInformation
Imports System.Text.RegularExpressions
Imports AVAK.Core

Namespace Network

    Public Class VpnEntry
        Public Property Name As String = ""
        Public Property ServerAddress As String = ""
        Public Property TunnelType As String = ""
        Public Property Connected As Boolean = False
        Public Property AllUsers As Boolean = False
    End Class

    ''' <summary>
    ''' Drives the VPN connections Windows already knows about (Settings > VPN).
    ''' AVAK does not implement a tunnel of its own - it manages and dials real
    ''' Windows RAS/VPN profiles via rasdial and the VpnClient PowerShell module.
    ''' </summary>
    Public NotInheritable Class VpnManager

        Private Sub New()
        End Sub

        Public Shared Function List() As List(Of VpnEntry)
            Dim items As New List(Of VpnEntry)()

            ' Preferred: the VpnClient module gives type + server + status
            Try
                Dim ps = Elevation.PowerShell(
                    "Get-VpnConnection -AllUserConnection -ErrorAction SilentlyContinue | " &
                    "ForEach-Object { ""$($_.Name)|$($_.ServerAddress)|$($_.TunnelType)|$($_.ConnectionStatus)|1"" }; " &
                    "Get-VpnConnection -ErrorAction SilentlyContinue | " &
                    "ForEach-Object { ""$($_.Name)|$($_.ServerAddress)|$($_.TunnelType)|$($_.ConnectionStatus)|0"" }")

                For Each line In ps.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                    Dim parts = line.Split("|"c)
                    If parts.Length < 4 Then Continue For
                    If items.Any(Function(x) String.Equals(x.Name, parts(0), StringComparison.OrdinalIgnoreCase)) Then Continue For
                    items.Add(New VpnEntry With {
                        .Name = parts(0).Trim(),
                        .ServerAddress = parts(1).Trim(),
                        .TunnelType = parts(2).Trim(),
                        .Connected = parts(3).Trim().Equals("Connected", StringComparison.OrdinalIgnoreCase),
                        .AllUsers = parts.Length > 4 AndAlso parts(4).Trim() = "1"})
                Next
            Catch ex As Exception
                Logger.Warn("Get-VpnConnection failed: " & ex.Message)
            End Try

            ' Fallback: phonebook entries visible to rasdial
            If items.Count = 0 Then
                Try
                    Dim outp = Elevation.Capture("rasdial.exe", "")
                    Dim connectedNames = ParseRasdialConnected(outp)
                    For Each n In connectedNames
                        items.Add(New VpnEntry With {.Name = n, .Connected = True, .TunnelType = "RAS"})
                    Next
                Catch
                End Try
            End If

            ' cross-check against live tunnel adapters
            Try
                Dim up = NetworkInterface.GetAllNetworkInterfaces().
                    Where(Function(n) n.OperationalStatus = OperationalStatus.Up AndAlso
                                      (n.NetworkInterfaceType = NetworkInterfaceType.Ppp OrElse
                                       n.NetworkInterfaceType = NetworkInterfaceType.Tunnel)).
                    Select(Function(n) n.Name).ToList()
                For Each e In items
                    If up.Any(Function(n) n.IndexOf(e.Name, StringComparison.OrdinalIgnoreCase) >= 0) Then e.Connected = True
                Next
            Catch
            End Try

            Return items.OrderByDescending(Function(e) e.Connected).ThenBy(Function(e) e.Name).ToList()
        End Function

        Private Shared Function ParseRasdialConnected(text As String) As List(Of String)
            Dim l As New List(Of String)()
            If String.IsNullOrWhiteSpace(text) Then Return l
            For Each line In text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                Dim t = line.Trim()
                If t.Length = 0 Then Continue For
                If t.StartsWith("No connections", StringComparison.OrdinalIgnoreCase) Then Continue For
                If t.StartsWith("Command completed", StringComparison.OrdinalIgnoreCase) Then Continue For
                If t.StartsWith("Connected to", StringComparison.OrdinalIgnoreCase) Then
                    l.Add(t.Substring("Connected to".Length).Trim())
                ElseIf Not t.Contains(" ") Then
                    l.Add(t)
                End If
            Next
            Return l
        End Function

        Public Shared ReadOnly Property IsAnyConnected As Boolean
            Get
                Try
                    Return NetworkInterface.GetAllNetworkInterfaces().Any(
                        Function(n) n.OperationalStatus = OperationalStatus.Up AndAlso
                                    (n.NetworkInterfaceType = NetworkInterfaceType.Ppp OrElse
                                     n.NetworkInterfaceType = NetworkInterfaceType.Tunnel))
                Catch
                    Return False
                End Try
            End Get
        End Property

        Public Shared Function Connect(name As String, Optional user As String = "",
                                       Optional password As String = "") As (Ok As Boolean, Message As String)
            If String.IsNullOrWhiteSpace(name) Then Return (False, "No VPN selected.")
            Try
                Dim args = """" & name & """"
                If Not String.IsNullOrEmpty(user) Then
                    args &= " " & user & " " & If(String.IsNullOrEmpty(password), "*", password)
                End If
                Dim outp = Elevation.Capture("rasdial.exe", args, 60000)
                Dim ok = outp.IndexOf("Successfully connected", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                         outp.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0
                Logger.Info($"VPN connect '{name}': {If(ok, "ok", "failed")}")
                If ok Then Security.EventLogStore.Add("network", "VPN connected", name)
                Return (ok, If(ok, "Connected to " & name & ".", CleanRasMessage(outp)))
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

        Public Shared Function Disconnect(name As String) As (Ok As Boolean, Message As String)
            Try
                Dim outp = Elevation.Capture("rasdial.exe", """" & name & """ /disconnect", 30000)
                Security.EventLogStore.Add("network", "VPN disconnected", name)
                Return (True, "Disconnected from " & name & ".")
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

        ''' <summary>Creates a Windows VPN profile (IKEv2/L2TP/SSTP/Automatic).</summary>
        Public Shared Function CreateProfile(name As String, server As String, tunnel As String) As (Ok As Boolean, Message As String)
            If String.IsNullOrWhiteSpace(name) OrElse String.IsNullOrWhiteSpace(server) Then
                Return (False, "Name and server address are required.")
            End If
            Dim t = If(String.IsNullOrWhiteSpace(tunnel), "Automatic", tunnel)
            Dim script = $"Add-VpnConnection -Name '{Esc(name)}' -ServerAddress '{Esc(server)}' " &
                         $"-TunnelType {t} -AuthenticationMethod MSChapv2 -EncryptionLevel Required " &
                         "-RememberCredential -PassThru -Force -ErrorAction Stop | Out-Null; 'OK'"
            Dim outp = Elevation.PowerShell(script, 40000)
            If outp.Contains("OK") Then
                Logger.Info("Created VPN profile " & name)
                Return (True, "VPN profile '" & name & "' created.")
            End If
            Return (False, If(String.IsNullOrWhiteSpace(outp), "PowerShell did not report success.", outp.Trim()))
        End Function

        Public Shared Function DeleteProfile(name As String) As (Ok As Boolean, Message As String)
            Dim outp = Elevation.PowerShell(
                $"Remove-VpnConnection -Name '{Esc(name)}' -Force -ErrorAction SilentlyContinue; 'OK'", 30000)
            Return If(outp.Contains("OK"), (True, "Profile removed."), (False, "Could not remove that profile."))
        End Function

        Public Shared Sub OpenWindowsVpnSettings()
            Try
                Process.Start(New ProcessStartInfo("ms-settings:network-vpn") With {.UseShellExecute = True})
            Catch ex As Exception
                Logger.Warn("Could not open VPN settings: " & ex.Message)
            End Try
        End Sub

        Private Shared Function Esc(s As String) As String
            Return If(s, "").Replace("'", "''")
        End Function

        Private Shared Function CleanRasMessage(raw As String) As String
            If String.IsNullOrWhiteSpace(raw) Then Return "rasdial returned no output."
            Dim m = Regex.Match(raw, "Remote Access error (\d+)[^\r\n]*")
            If m.Success Then Return m.Value.Trim()
            Return raw.Trim().Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
        End Function

    End Class

End Namespace
