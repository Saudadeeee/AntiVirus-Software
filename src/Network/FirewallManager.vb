Option Strict Off

Imports AVAK.Core

Namespace Network

    Public Class FirewallState
        Public Property DomainEnabled As Boolean
        Public Property PrivateEnabled As Boolean
        Public Property PublicEnabled As Boolean
        Public Property Available As Boolean
        Public Property Error_ As String = ""

        Public ReadOnly Property AllOn As Boolean
            Get
                Return Available AndAlso DomainEnabled AndAlso PrivateEnabled AndAlso PublicEnabled
            End Get
        End Property

        Public ReadOnly Property AnyOff As Boolean
            Get
                Return Available AndAlso (Not DomainEnabled OrElse Not PrivateEnabled OrElse Not PublicEnabled)
            End Get
        End Property

        Public ReadOnly Property Summary As String
            Get
                If Not Available Then Return "Firewall state unavailable"
                If AllOn Then Return "All profiles protected"
                Dim off As New List(Of String)()
                If Not DomainEnabled Then off.Add("Domain")
                If Not PrivateEnabled Then off.Add("Private")
                If Not PublicEnabled Then off.Add("Public")
                Return String.Join(", ", off) & " profile off"
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Reads Windows Firewall state through the HNetCfg.FwPolicy2 COM object
    ''' (works without elevation) and changes it through netsh with a UAC prompt.
    ''' </summary>
    Public NotInheritable Class FirewallManager

        Private Sub New()
        End Sub

        Private Const NET_FW_PROFILE2_DOMAIN As Integer = 1
        Private Const NET_FW_PROFILE2_PRIVATE As Integer = 2
        Private Const NET_FW_PROFILE2_PUBLIC As Integer = 4

        Public Shared Function Read() As FirewallState
            Dim st As New FirewallState()
            Try
                Dim t = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
                If t Is Nothing Then
                    st.Error_ = "Windows Firewall COM interface not registered."
                    Return st
                End If
                Dim policy As Object = Activator.CreateInstance(t)
                st.DomainEnabled = CBool(policy.FirewallEnabled(NET_FW_PROFILE2_DOMAIN))
                st.PrivateEnabled = CBool(policy.FirewallEnabled(NET_FW_PROFILE2_PRIVATE))
                st.PublicEnabled = CBool(policy.FirewallEnabled(NET_FW_PROFILE2_PUBLIC))
                st.Available = True
                Runtime.InteropServices.Marshal.ReleaseComObject(policy)
            Catch ex As Exception
                st.Error_ = ex.Message
                Logger.Warn("Firewall read failed: " & ex.Message)
            End Try
            Return st
        End Function

        ''' <summary>Turns every profile on or off. Prompts for elevation when needed.</summary>
        Public Shared Function SetAll(enable As Boolean) As (Ok As Boolean, Message As String)
            Dim verb = If(enable, "on", "off")
            Dim ok = Elevation.RunElevated("netsh.exe", $"advfirewall set allprofiles state {verb}")
            If ok Then
                Logger.Warn($"Windows Firewall turned {verb} by the user")
                Security.EventLogStore.Add("firewall", "Firewall turned " & verb, "All profiles",
                                           If(enable, Security.Severity.Clean, Security.Severity.High))
                Return (True, "Firewall turned " & verb & " for all profiles.")
            End If
            Return (False, "Could not change the firewall. Administrator rights are required.")
        End Function

        Public Shared Function SetProfile(profile As String, enable As Boolean) As (Ok As Boolean, Message As String)
            Dim p = profile.ToLowerInvariant()
            If p <> "domain" AndAlso p <> "private" AndAlso p <> "public" Then Return (False, "Unknown profile.")
            Dim verb = If(enable, "on", "off")
            Dim ok = Elevation.RunElevated("netsh.exe", $"advfirewall set {p}profile state {verb}")
            Return If(ok,
                      (True, $"{profile} profile turned {verb}."),
                      (False, "Could not change the firewall. Administrator rights are required."))
        End Function

        Public Shared Function RestoreDefaults() As (Ok As Boolean, Message As String)
            Dim ok = Elevation.RunElevated("netsh.exe", "advfirewall reset")
            Return If(ok,
                      (True, "Firewall rules reset to Windows defaults."),
                      (False, "Reset failed - administrator rights are required."))
        End Function

        ''' <summary>Blocks a program's inbound + outbound traffic by full path.</summary>
        Public Shared Function BlockProgram(exePath As String, ruleName As String) As (Ok As Boolean, Message As String)
            If String.IsNullOrWhiteSpace(exePath) Then Return (False, "No program specified.")
            Dim safeName = ruleName.Replace("""", "")
            Dim okOut = Elevation.RunElevated("netsh.exe",
                $"advfirewall firewall add rule name=""AVAK block {safeName} (out)"" dir=out action=block program=""{exePath}"" enable=yes")
            Dim okIn = Elevation.RunElevated("netsh.exe",
                $"advfirewall firewall add rule name=""AVAK block {safeName} (in)"" dir=in action=block program=""{exePath}"" enable=yes")
            If okOut OrElse okIn Then
                Security.EventLogStore.Add("firewall", "Blocked program", exePath, Security.Severity.Medium)
                Return (True, "Firewall rules added for " & IO.Path.GetFileName(exePath) & ".")
            End If
            Return (False, "Could not add the rule. Administrator rights are required.")
        End Function

        Public Shared Function RemoveAvakRules() As (Ok As Boolean, Message As String)
            Dim outp = Elevation.PowerShell(
                "Get-NetFirewallRule -DisplayName 'AVAK block*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue; 'done'")
            If outp.Contains("done") Then Return (True, "AVAK firewall rules removed.")
            Return (False, "Could not remove the rules (administrator rights required).")
        End Function

        Public Shared Function CountAvakRules() As Integer
            Try
                Dim outp = Elevation.PowerShell(
                    "(Get-NetFirewallRule -DisplayName 'AVAK block*' -ErrorAction SilentlyContinue | Measure-Object).Count")
                Dim n = 0
                Integer.TryParse(outp.Trim(), n)
                Return n
            Catch
                Return 0
            End Try
        End Function

    End Class

End Namespace
