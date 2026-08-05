Imports System.Diagnostics
Imports System.IO
Imports System.Security.Cryptography.X509Certificates
Imports AVAK.Core
Imports AVAK.Security

Namespace SystemInfo

    Public Class ProcessRow
        Public Property Pid As Integer
        Public Property Name As String = ""
        Public Property ImagePath As String = ""
        Public Property WorkingSetBytes As Long
        Public Property Threads As Integer
        Public Property StartedAt As DateTime = DateTime.MinValue
        Public Property Signed As Boolean? = Nothing
        Public Property Signer As String = ""
        Public Property Description As String = ""
        Public Property IsProtected As Boolean = False

        Public ReadOnly Property TrustLabel As String
            Get
                If IsProtected Then Return "System"
                If Not Signed.HasValue Then Return "Unknown"
                Return If(Signed.Value, "Signed", "Unsigned")
            End Get
        End Property
    End Class

    ''' <summary>Live process list with Authenticode verification and on-demand scanning.</summary>
    Public NotInheritable Class ProcessInspector

        Private Sub New()
        End Sub

        Private Shared ReadOnly SignatureCache As New Dictionary(Of String, (Signed As Boolean, Signer As String))(
            StringComparer.OrdinalIgnoreCase)

        Public Shared Function Snapshot(Optional verifySignatures As Boolean = True) As List(Of ProcessRow)
            Dim rows As New List(Of ProcessRow)()
            For Each p In Process.GetProcesses()
                Dim row As New ProcessRow With {.Pid = p.Id, .Name = p.ProcessName}
                Try
                    row.WorkingSetBytes = p.WorkingSet64
                    row.Threads = p.Threads.Count
                Catch
                End Try
                Try
                    row.StartedAt = p.StartTime
                Catch
                End Try
                Try
                    row.ImagePath = If(p.MainModule?.FileName, "")
                    Dim fv = p.MainModule?.FileVersionInfo
                    row.Description = If(fv?.FileDescription, "")
                Catch
                    row.IsProtected = True
                End Try

                If verifySignatures AndAlso Not String.IsNullOrEmpty(row.ImagePath) Then
                    Dim sig = VerifySignature(row.ImagePath)
                    row.Signed = sig.Signed
                    row.Signer = sig.Signer
                End If

                rows.Add(row)
                p.Dispose()
            Next
            Return rows.OrderByDescending(Function(r) r.WorkingSetBytes).ToList()
        End Function

        ''' <summary>Authenticode check. Cached because certificate parsing is not cheap.</summary>
        Public Shared Function VerifySignature(path As String) As (Signed As Boolean, Signer As String)
            SyncLock SignatureCache
                Dim hit As (Signed As Boolean, Signer As String) = (False, "")
                If SignatureCache.TryGetValue(path, hit) Then Return hit
            End SyncLock

            Dim result As (Signed As Boolean, Signer As String) = (False, "")
            Try
                If File.Exists(path) Then
                    Using cert = X509Certificate.CreateFromSignedFile(path)
                        Dim c2 As New X509Certificate2(cert)
                        Dim cn = c2.GetNameInfo(X509NameType.SimpleName, False)
                        result = (True, If(String.IsNullOrWhiteSpace(cn), c2.Subject, cn))
                    End Using
                End If
            Catch
                result = (False, "")
            End Try

            SyncLock SignatureCache
                SignatureCache(path) = result
            End SyncLock
            Return result
        End Function

        Public Shared Function Kill(pid As Integer) As (Ok As Boolean, Message As String)
            Try
                Using p = Process.GetProcessById(pid)
                    p.Kill(True)
                    p.WaitForExit(4000)
                End Using
                Logger.Warn($"Terminated PID {pid}")
                Return (True, "Process terminated.")
            Catch ex As UnauthorizedAccessException
                Return (False, "Access denied - restart AVAK as administrator to end this process.")
            Catch ex As ArgumentException
                Return (False, "That process already exited.")
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

        ''' <summary>Scans the on-disk image of one process against the signature engine.</summary>
        Public Shared Function ScanImage(path As String) As Detection
            Try
                If String.IsNullOrEmpty(path) OrElse Not File.Exists(path) Then Return Nothing
                Return ScanEngine.InspectFile(path, New FileInfo(path), AppSettings.Current)
            Catch
                Return Nothing
            End Try
        End Function

        Public Shared Sub OpenLocation(path As String)
            Try
                If String.IsNullOrEmpty(path) OrElse Not File.Exists(path) Then Return
                Process.Start(New ProcessStartInfo("explorer.exe", "/select,""" & path & """") With {.UseShellExecute = True})
            Catch ex As Exception
                Logger.Warn("OpenLocation failed: " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
