Imports System.IO
Imports System.Security.Cryptography
Imports AVAK.Core

Namespace Security

    Public Class QuarantineItem
        Public Property Id As String = Guid.NewGuid().ToString("N")
        Public Property OriginalPath As String = ""
        Public Property ThreatName As String = ""
        Public Property Severity As Severity = Severity.Medium
        Public Property Sha256 As String = ""
        Public Property SizeBytes As Long = 0
        Public Property QuarantinedAt As DateTime = DateTime.Now
        Public Property Source As DetectionSource = DetectionSource.Signature
        Public Property Reasons As New List(Of String)()
        ''' <summary>DPAPI-protected AES key + IV, base64.</summary>
        Public Property ProtectedKey As String = ""

        Public ReadOnly Property VaultFile As String
            Get
                Return Path.Combine(AppPaths.Quarantine, Id & ".avq")
            End Get
        End Property

        Public ReadOnly Property FileName As String
            Get
                Try
                    Return Path.GetFileName(OriginalPath)
                Catch
                    Return OriginalPath
                End Try
            End Get
        End Property
    End Class

    Public Class QuarantineIndex
        Public Property Items As New List(Of QuarantineItem)()
    End Class

    ''' <summary>
    ''' Moves detected files into an encrypted vault so they can no longer run,
    ''' while staying restorable. Content is AES-256-CBC encrypted with a random
    ''' per-item key; that key is stored DPAPI-protected for the current user.
    ''' </summary>
    Public NotInheritable Class QuarantineManager

        Private Sub New()
        End Sub

        Private Shared ReadOnly Gate As New Object()
        Private Shared _index As QuarantineIndex

        Public Shared Event Changed As EventHandler

        Private Shared ReadOnly Property Index As QuarantineIndex
            Get
                SyncLock Gate
                    If _index Is Nothing Then
                        _index = Json.Load(Of QuarantineIndex)(AppPaths.QuarantineIndex)
                        If _index Is Nothing Then _index = New QuarantineIndex()
                    End If
                    Return _index
                End SyncLock
            End Get
        End Property

        Private Shared Sub Persist()
            SyncLock Gate
                Json.Save(AppPaths.QuarantineIndex, _index)
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        Public Shared Function All() As List(Of QuarantineItem)
            SyncLock Gate
                Return Index.Items.OrderByDescending(Function(i) i.QuarantinedAt).ToList()
            End SyncLock
        End Function

        Public Shared ReadOnly Property Count As Integer
            Get
                SyncLock Gate
                    Return Index.Items.Count
                End SyncLock
            End Get
        End Property

        Public Shared ReadOnly Property TotalBytes As Long
            Get
                SyncLock Gate
                    Return Index.Items.Sum(Function(i) i.SizeBytes)
                End SyncLock
            End Get
        End Property

        ' -- quarantine -------------------------------------------------------

        Public Class OperationResult
            Public Property Ok As Boolean
            Public Property Message As String = ""
            Public Property NeedsElevation As Boolean = False
        End Class

        Public Shared Function Quarantine(d As Detection) As OperationResult
            Try
                If d Is Nothing OrElse String.IsNullOrEmpty(d.FilePath) Then
                    Return New OperationResult With {.Ok = False, .Message = "No file specified."}
                End If
                Dim fi As New FileInfo(d.FilePath)
                If Not fi.Exists Then
                    Return New OperationResult With {.Ok = False, .Message = "File no longer exists."}
                End If

                Dim item As New QuarantineItem With {
                    .OriginalPath = fi.FullName,
                    .ThreatName = d.ThreatName,
                    .Severity = d.Severity,
                    .Sha256 = If(String.IsNullOrEmpty(d.Sha256), SignatureDatabase.Sha256File(fi.FullName), d.Sha256),
                    .SizeBytes = fi.Length,
                    .Source = d.Source,
                    .Reasons = If(d.Reasons, New List(Of String)())
                }

                Using alg = Aes.Create()
                    alg.KeySize = 256
                    alg.GenerateKey()
                    alg.GenerateIV()

                    Dim keyBlob(alg.Key.Length + alg.IV.Length - 1) As Byte
                    Array.Copy(alg.Key, 0, keyBlob, 0, alg.Key.Length)
                    Array.Copy(alg.IV, 0, keyBlob, alg.Key.Length, alg.IV.Length)
                    item.ProtectedKey = Convert.ToBase64String(
                        ProtectedData.Protect(keyBlob, Nothing, DataProtectionScope.CurrentUser))

                    Using src As New FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Using dst As New FileStream(item.VaultFile, FileMode.Create, FileAccess.Write, FileShare.None)
                            Using cs As New CryptoStream(dst, alg.CreateEncryptor(), CryptoStreamMode.Write)
                                src.CopyTo(cs)
                            End Using
                        End Using
                    End Using
                End Using

                ' remove the original only after the vault copy is safely written
                Try
                    fi.Attributes = FileAttributes.Normal
                    fi.Delete()
                Catch ex As UnauthorizedAccessException
                    Try
                        File.Delete(item.VaultFile)
                    Catch
                    End Try
                    Return New OperationResult With {
                        .Ok = False, .NeedsElevation = True,
                        .Message = "Access denied. Restart AVAK as administrator to quarantine this file."}
                Catch ex As IOException
                    Try
                        File.Delete(item.VaultFile)
                    Catch
                    End Try
                    Return New OperationResult With {
                        .Ok = False, .Message = "The file is in use by another program: " & ex.Message}
                End Try

                SyncLock Gate
                    Index.Items.Add(item)
                End SyncLock
                Persist()
                d.Quarantined = True

                Logger.Info($"Quarantined {item.OriginalPath} ({item.ThreatName})")
                Return New OperationResult With {.Ok = True, .Message = "Moved to quarantine."}

            Catch ex As Exception
                Logger.Error("Quarantine failed for " & d.FilePath, ex)
                Return New OperationResult With {.Ok = False, .Message = ex.Message}
            End Try
        End Function

        ' -- restore ----------------------------------------------------------

        Public Shared Function Restore(id As String, Optional targetPath As String = Nothing) As OperationResult
            Try
                Dim item As QuarantineItem
                SyncLock Gate
                    item = Index.Items.FirstOrDefault(Function(i) i.Id = id)
                End SyncLock
                If item Is Nothing Then Return New OperationResult With {.Ok = False, .Message = "Item not found."}
                If Not File.Exists(item.VaultFile) Then
                    Return New OperationResult With {.Ok = False, .Message = "The vault file is missing."}
                End If

                Dim dest = If(String.IsNullOrWhiteSpace(targetPath), item.OriginalPath, targetPath)
                Dim dir = Path.GetDirectoryName(dest)
                If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
                If File.Exists(dest) Then
                    Return New OperationResult With {.Ok = False, .Message = "A file already exists at the restore location."}
                End If

                Dim keyBlob = ProtectedData.Unprotect(
                    Convert.FromBase64String(item.ProtectedKey), Nothing, DataProtectionScope.CurrentUser)

                Using alg = Aes.Create()
                    alg.KeySize = 256
                    Dim key(31) As Byte
                    Dim iv(15) As Byte
                    Array.Copy(keyBlob, 0, key, 0, 32)
                    Array.Copy(keyBlob, 32, iv, 0, 16)
                    alg.Key = key
                    alg.IV = iv

                    Using src As New FileStream(item.VaultFile, FileMode.Open, FileAccess.Read)
                        Using cs As New CryptoStream(src, alg.CreateDecryptor(), CryptoStreamMode.Read)
                            Using dst As New FileStream(dest, FileMode.CreateNew, FileAccess.Write)
                                cs.CopyTo(dst)
                            End Using
                        End Using
                    End Using
                End Using

                Try
                    File.Delete(item.VaultFile)
                Catch
                End Try
                SyncLock Gate
                    Index.Items.Remove(item)
                End SyncLock
                Persist()

                Logger.Warn($"Restored {dest} from quarantine ({item.ThreatName})")
                Return New OperationResult With {.Ok = True, .Message = "Restored to " & dest}

            Catch ex As CryptographicException
                Return New OperationResult With {
                    .Ok = False,
                    .Message = "Could not decrypt. Quarantine keys are bound to the Windows user that created them."}
            Catch ex As Exception
                Logger.Error("Restore failed", ex)
                Return New OperationResult With {.Ok = False, .Message = ex.Message}
            End Try
        End Function

        ' -- delete -----------------------------------------------------------

        Public Shared Function DeleteForever(id As String) As OperationResult
            Try
                Dim item As QuarantineItem
                SyncLock Gate
                    item = Index.Items.FirstOrDefault(Function(i) i.Id = id)
                End SyncLock
                If item Is Nothing Then Return New OperationResult With {.Ok = False, .Message = "Item not found."}

                Try
                    If File.Exists(item.VaultFile) Then Shred(item.VaultFile)
                Catch ex As Exception
                    Logger.Warn("Could not shred vault file: " & ex.Message)
                End Try

                SyncLock Gate
                    Index.Items.Remove(item)
                End SyncLock
                Persist()
                Logger.Info($"Deleted {item.FileName} from quarantine")
                Return New OperationResult With {.Ok = True, .Message = "Deleted permanently."}
            Catch ex As Exception
                Return New OperationResult With {.Ok = False, .Message = ex.Message}
            End Try
        End Function

        Public Shared Function Purge() As Integer
            Dim ids As List(Of String)
            SyncLock Gate
                ids = Index.Items.Select(Function(i) i.Id).ToList()
            End SyncLock
            Dim n = 0
            For Each id In ids
                If DeleteForever(id).Ok Then n += 1
            Next
            Return n
        End Function

        ''' <summary>Overwrites the file with random bytes before unlinking it.</summary>
        Private Shared Sub Shred(path As String)
            Dim total = New FileInfo(path).Length
            If total > 0 AndAlso total < 64L * 1024L * 1024L Then
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)
                    Dim buf(65535) As Byte
                    RandomNumberGenerator.Fill(buf)
                    Dim written As Long = 0
                    While written < total
                        Dim n = CInt(Math.Min(buf.Length, total - written))
                        fs.Write(buf, 0, n)
                        written += n
                    End While
                    fs.Flush(True)
                End Using
            End If
            File.SetAttributes(path, FileAttributes.Normal)
            File.Delete(path)
        End Sub

        ''' <summary>Drops index entries whose vault file vanished (manual folder cleanup).</summary>
        Public Shared Sub Reconcile()
            Dim removed = 0
            SyncLock Gate
                Dim gone = Index.Items.Where(Function(i) Not File.Exists(i.VaultFile)).ToList()
                For Each g In gone
                    Index.Items.Remove(g)
                    removed += 1
                Next
            End SyncLock
            If removed > 0 Then
                Logger.Warn($"Quarantine index: removed {removed} orphaned entries")
                Persist()
            End If
        End Sub

    End Class

End Namespace
