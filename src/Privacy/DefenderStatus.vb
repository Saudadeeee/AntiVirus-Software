Imports System.Management
Imports AVAK.Core

Namespace Privacy

    Public Class DefenderInfo
        Public Property Available As Boolean = False
        Public Property AntivirusEnabled As Boolean
        Public Property RealTimeProtectionEnabled As Boolean
        Public Property AntispywareEnabled As Boolean
        Public Property TamperProtected As Boolean
        Public Property SignatureVersion As String = ""
        Public Property SignatureAge As Integer = -1
        Public Property LastQuickScan As DateTime = DateTime.MinValue
        Public Property LastFullScan As DateTime = DateTime.MinValue
        Public Property Error_ As String = ""

        Public ReadOnly Property Healthy As Boolean
            Get
                Return Available AndAlso AntivirusEnabled AndAlso RealTimeProtectionEnabled
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Reads Microsoft Defender status from the MSFT_MpComputerStatus WMI class.
    ''' AVAK is designed to sit next to Defender, not replace it, so this is
    ''' surfaced on the dashboard as part of the overall protection score.
    ''' </summary>
    Public NotInheritable Class DefenderStatus

        Private Sub New()
        End Sub

        Private Shared _cache As DefenderInfo
        Private Shared _cachedAt As DateTime = DateTime.MinValue

        Public Shared Function Read(Optional force As Boolean = False) As DefenderInfo
            If Not force AndAlso _cache IsNot Nothing AndAlso (DateTime.Now - _cachedAt).TotalSeconds < 60 Then
                Return _cache
            End If

            Dim info As New DefenderInfo()
            Try
                Using searcher As New ManagementObjectSearcher(
                    "root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpComputerStatus")
                    Using results = searcher.Get()
                        For Each o As ManagementBaseObject In results
                            Try
                                info.Available = True
                                info.AntivirusEnabled = Bool(o, "AntivirusEnabled")
                                info.RealTimeProtectionEnabled = Bool(o, "RealTimeProtectionEnabled")
                                info.AntispywareEnabled = Bool(o, "AntispywareEnabled")
                                info.TamperProtected = Bool(o, "IsTamperProtected")
                                info.SignatureVersion = Str(o, "AntivirusSignatureVersion")
                                info.SignatureAge = Int_(o, "AntivirusSignatureAge")
                                info.LastQuickScan = Date_(o, "QuickScanEndTime")
                                info.LastFullScan = Date_(o, "FullScanEndTime")
                            Finally
                                o.Dispose()
                            End Try
                            Exit For
                        Next
                    End Using
                End Using
            Catch ex As Exception
                info.Available = False
                info.Error_ = ex.Message
                Logger.Warn("Defender status unavailable: " & ex.Message)
            End Try

            _cache = info
            _cachedAt = DateTime.Now
            Return info
        End Function

        Public Shared Sub OpenWindowsSecurity()
            Try
                Process.Start(New ProcessStartInfo("windowsdefender://") With {.UseShellExecute = True})
            Catch
                Try
                    Process.Start(New ProcessStartInfo("ms-settings:windowsdefender") With {.UseShellExecute = True})
                Catch ex As Exception
                    Logger.Warn("Could not open Windows Security: " & ex.Message)
                End Try
            End Try
        End Sub

        ''' <summary>Adds AVAK's quarantine folder to Defender's exclusions so the two do not fight.</summary>
        Public Shared Function ExcludeQuarantineFolder() As (Ok As Boolean, Message As String)
            Dim outp = Elevation.PowerShell(
                $"Add-MpPreference -ExclusionPath '{AppPaths.Quarantine.Replace("'", "''")}' -ErrorAction Stop; 'OK'", 30000)
            If outp.Contains("OK") Then Return (True, "Defender will now ignore the AVAK quarantine folder.")
            Return (False, "Could not add the exclusion. Administrator rights are required.")
        End Function

        Private Shared Function Bool(o As ManagementBaseObject, p As String) As Boolean
            Try
                Return Convert.ToBoolean(o(p))
            Catch
                Return False
            End Try
        End Function

        Private Shared Function Str(o As ManagementBaseObject, p As String) As String
            Try
                Return Convert.ToString(o(p))
            Catch
                Return ""
            End Try
        End Function

        Private Shared Function Int_(o As ManagementBaseObject, p As String) As Integer
            Try
                Return Convert.ToInt32(o(p))
            Catch
                Return -1
            End Try
        End Function

        Private Shared Function Date_(o As ManagementBaseObject, p As String) As DateTime
            Try
                Dim v = o(p)
                If v Is Nothing Then Return DateTime.MinValue
                Dim s = Convert.ToString(v)
                If s.Length >= 14 Then Return ManagementDateTimeConverter.ToDateTime(s)
                Return Convert.ToDateTime(v)
            Catch
                Return DateTime.MinValue
            End Try
        End Function

    End Class

    Public Class HostsFinding
        Public Property Line As String = ""
        Public Property Host As String = ""
        Public Property Address As String = ""
        Public Property Suspicious As Boolean = False
        Public Property Why As String = ""
    End Class

    ''' <summary>Flags hosts-file entries that redirect security or update domains.</summary>
    Public NotInheritable Class HostsGuard

        Private Sub New()
        End Sub

        Private Shared ReadOnly Sensitive As String() = {
            "windowsupdate", "microsoft.com", "windows.com", "defender", "avast", "avg", "kaspersky",
            "bitdefender", "malwarebytes", "eset", "norton", "mcafee", "sophos", "virustotal",
            "google.com", "githubusercontent", "symantec", "trendmicro"}

        Public Shared Function Path_() As String
            Return IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers\etc\hosts")
        End Function

        Public Shared Function Inspect() As List(Of HostsFinding)
            Dim findings As New List(Of HostsFinding)()
            Try
                Dim p = Path_()
                If Not IO.File.Exists(p) Then Return findings

                For Each raw In IO.File.ReadAllLines(p)
                    Dim line = raw.Trim()
                    If line.Length = 0 OrElse line.StartsWith("#") Then Continue For
                    Dim parts = line.Split(New Char() {" "c, ChrW(9)}, StringSplitOptions.RemoveEmptyEntries)
                    If parts.Length < 2 Then Continue For

                    Dim addr = parts(0)
                    For i = 1 To parts.Length - 1
                        Dim host = parts(i)
                        If host.StartsWith("#") Then Exit For
                        Dim f As New HostsFinding With {.Line = line, .Address = addr, .Host = host}
                        Dim lowerHost = host.ToLowerInvariant()
                        If Sensitive.Any(Function(s) lowerHost.Contains(s)) Then
                            f.Suspicious = True
                            f.Why = "Redirects a security or update domain"
                        ElseIf Not addr.StartsWith("127.") AndAlso Not addr.StartsWith("0.0.0.0") AndAlso
                               Not addr.StartsWith("::1") Then
                            f.Suspicious = True
                            f.Why = "Points a hostname at an external address"
                        End If
                        findings.Add(f)
                    Next
                Next
            Catch ex As Exception
                Logger.Warn("Hosts file read failed: " & ex.Message)
            End Try
            Return findings
        End Function

        Public Shared Sub OpenInNotepad()
            Try
                Process.Start(New ProcessStartInfo("notepad.exe", Path_()) With {.UseShellExecute = True})
            Catch ex As Exception
                Logger.Warn("Could not open hosts file: " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
