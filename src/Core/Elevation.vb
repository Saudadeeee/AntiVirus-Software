Imports System.Security.Principal

Namespace Core

    ''' <summary>UAC helpers. AVAK runs unelevated and only asks when an action truly needs it.</summary>
    Public NotInheritable Class Elevation

        Private Sub New()
        End Sub

        Private Shared _cached As Boolean?

        Public Shared ReadOnly Property IsAdmin As Boolean
            Get
                If _cached.HasValue Then Return _cached.Value
                Try
                    Using id = WindowsIdentity.GetCurrent()
                        _cached = New WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator)
                    End Using
                Catch
                    _cached = False
                End Try
                Return _cached.Value
            End Get
        End Property

        ''' <summary>Restarts AVAK elevated. Returns False if the user dismissed the UAC prompt.</summary>
        Public Shared Function Relaunch(Optional args As String = "") As Boolean
            If IsAdmin Then Return True
            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = AppPaths.ExecutablePath,
                    .Arguments = args,
                    .UseShellExecute = True,
                    .Verb = "runas"
                }
                Process.Start(psi)
                Return True
            Catch ex As Exception
                Logger.Warn("Elevation declined: " & ex.Message)
                Return False
            End Try
        End Function

        ''' <summary>Runs a console command elevated and waits. Used by the firewall module.</summary>
        Public Shared Function RunElevated(fileName As String, arguments As String,
                                           Optional timeoutMs As Integer = 20000) As Boolean
            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = fileName,
                    .Arguments = arguments,
                    .UseShellExecute = True,
                    .CreateNoWindow = True,
                    .WindowStyle = ProcessWindowStyle.Hidden,
                    .Verb = If(IsAdmin, "", "runas")
                }
                Using p = Process.Start(psi)
                    If p Is Nothing Then Return False
                    If Not p.WaitForExit(timeoutMs) Then Return False
                    Return p.ExitCode = 0
                End Using
            Catch ex As Exception
                Logger.Warn("RunElevated failed (" & fileName & " " & arguments & "): " & ex.Message)
                Return False
            End Try
        End Function

        ''' <summary>Runs a command without elevation and captures stdout.</summary>
        Public Shared Function Capture(fileName As String, arguments As String,
                                       Optional timeoutMs As Integer = 20000) As String
            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = fileName,
                    .Arguments = arguments,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True,
                    .StandardOutputEncoding = Text.Encoding.UTF8
                }
                Using p = Process.Start(psi)
                    If p Is Nothing Then Return ""
                    Dim outp = p.StandardOutput.ReadToEnd()
                    p.WaitForExit(timeoutMs)
                    Return outp
                End Using
            Catch ex As Exception
                Logger.Warn("Capture failed (" & fileName & "): " & ex.Message)
                Return ""
            End Try
        End Function

        Public Shared Function PowerShell(script As String, Optional timeoutMs As Integer = 25000) As String
            Dim encoded = Convert.ToBase64String(Text.Encoding.Unicode.GetBytes(script))
            Return Capture("powershell.exe",
                           "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " & encoded,
                           timeoutMs)
        End Function

    End Class

End Namespace
