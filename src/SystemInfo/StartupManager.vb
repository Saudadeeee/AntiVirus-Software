Imports System.IO
Imports Microsoft.Win32
Imports AVAK.Core

Namespace SystemInfo

    Public Class StartupEntry
        Public Property Name As String = ""
        Public Property Command As String = ""
        Public Property ExecutablePath As String = ""
        ''' <summary>HKCU\Run, HKLM\Run, HKCU\RunOnce, Startup folder, ...</summary>
        Public Property Location As String = ""
        Public Property Enabled As Boolean = True
        Public Property CanModify As Boolean = True
        Public Property Signer As String = ""
        Public Property Signed As Boolean? = Nothing
    End Class

    ''' <summary>
    ''' Reads and edits the real Windows autorun locations: the Run/RunOnce registry
    ''' keys for the current user and the machine, plus both Startup folders.
    ''' Disabling moves the value into an AVAK backup key instead of deleting it.
    ''' </summary>
    Public NotInheritable Class StartupManager

        Private Sub New()
        End Sub

        Private Const BackupKey As String = "SOFTWARE\AVAK\DisabledStartup"

        Private Structure Location_
            Public Root As RegistryKey
            Public SubKey As String
            Public Label As String
            Public NeedsAdmin As Boolean
        End Structure

        Private Shared Function Locations() As List(Of Location_)
            Return New List(Of Location_) From {
                New Location_ With {.Root = Registry.CurrentUser, .SubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                                    .Label = "HKCU\Run", .NeedsAdmin = False},
                New Location_ With {.Root = Registry.CurrentUser, .SubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                                    .Label = "HKCU\RunOnce", .NeedsAdmin = False},
                New Location_ With {.Root = Registry.LocalMachine, .SubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                                    .Label = "HKLM\Run", .NeedsAdmin = True},
                New Location_ With {.Root = Registry.LocalMachine, .SubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                                    .Label = "HKLM\RunOnce", .NeedsAdmin = True}
            }
        End Function

        Public Shared Function All(Optional verifySignatures As Boolean = True) As List(Of StartupEntry)
            Dim list As New List(Of StartupEntry)()

            For Each entryLoc In Locations()
                Try
                    Using k = entryLoc.Root.OpenSubKey(entryLoc.SubKey, False)
                        If k Is Nothing Then Continue For
                        For Each name In k.GetValueNames()
                            Dim cmd = Convert.ToString(k.GetValue(name, ""))
                            list.Add(New StartupEntry With {
                                .Name = name,
                                .Command = cmd,
                                .ExecutablePath = ExtractExe(cmd),
                                .Location = entryLoc.Label,
                                .Enabled = True,
                                .CanModify = Not entryLoc.NeedsAdmin OrElse Elevation.IsAdmin})
                        Next
                    End Using
                Catch ex As Exception
                    Logger.Warn("Startup read failed for " & entryLoc.Label & ": " & ex.Message)
                End Try
            Next

            ' disabled items we parked earlier
            Try
                Using k = Registry.CurrentUser.OpenSubKey(BackupKey, False)
                    If k IsNot Nothing Then
                        For Each name In k.GetValueNames()
                            Dim cmd = Convert.ToString(k.GetValue(name, ""))
                            Dim label = "HKCU\Run"
                            Dim realName = name
                            Dim sep = name.IndexOf("|"c)
                            If sep > 0 Then
                                label = name.Substring(0, sep)
                                realName = name.Substring(sep + 1)
                            End If
                            list.Add(New StartupEntry With {
                                .Name = realName,
                                .Command = cmd,
                                .ExecutablePath = ExtractExe(cmd),
                                .Location = label,
                                .Enabled = False,
                                .CanModify = True})
                        Next
                    End If
                End Using
            Catch
            End Try

            ' startup folders
            For Each pair In {(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder (user)"),
                              (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)")}
                Try
                    If String.IsNullOrEmpty(pair.Item1) OrElse Not Directory.Exists(pair.Item1) Then Continue For
                    For Each f In Directory.EnumerateFiles(pair.Item1)
                        Dim ext = Path.GetExtension(f)
                        If String.Equals(ext, ".ini", StringComparison.OrdinalIgnoreCase) Then Continue For
                        list.Add(New StartupEntry With {
                            .Name = Path.GetFileNameWithoutExtension(f),
                            .Command = f,
                            .ExecutablePath = f,
                            .Location = pair.Item2,
                            .Enabled = True,
                            .CanModify = True})
                    Next
                Catch
                End Try
            Next

            If verifySignatures Then
                For Each e In list
                    If String.IsNullOrEmpty(e.ExecutablePath) OrElse Not File.Exists(e.ExecutablePath) Then Continue For
                    Dim sig = ProcessInspector.VerifySignature(e.ExecutablePath)
                    e.Signed = sig.Signed
                    e.Signer = sig.Signer
                Next
            End If

            Return list.OrderBy(Function(e) e.Location).ThenBy(Function(e) e.Name).ToList()
        End Function

        Public Shared Function SetEnabled(entry As StartupEntry, enable As Boolean) As (Ok As Boolean, Message As String)
            Try
                If entry.Location.StartsWith("Startup folder", StringComparison.OrdinalIgnoreCase) Then
                    Return ToggleStartupFile(entry, enable)
                End If

                Dim entryLoc = Locations().FirstOrDefault(Function(l) l.Label = entry.Location)
                If entryLoc.Root Is Nothing Then Return (False, "Unknown startup location.")
                If entryLoc.NeedsAdmin AndAlso Not Elevation.IsAdmin Then
                    Return (False, "This entry is machine-wide. Restart AVAK as administrator to change it.")
                End If

                Dim backupName = entry.Location & "|" & entry.Name

                If enable Then
                    Using bk = Registry.CurrentUser.OpenSubKey(BackupKey, True)
                        If bk Is Nothing Then Return (False, "No saved copy of that entry.")
                        Dim cmd = Convert.ToString(bk.GetValue(backupName, ""))
                        If String.IsNullOrEmpty(cmd) Then Return (False, "No saved copy of that entry.")
                        Using k = entryLoc.Root.OpenSubKey(entryLoc.SubKey, True)
                            If k Is Nothing Then Return (False, "Registry key not available.")
                            k.SetValue(entry.Name, cmd, RegistryValueKind.String)
                        End Using
                        bk.DeleteValue(backupName, False)
                    End Using
                    Logger.Info($"Startup enabled: {entry.Name}")
                    Return (True, "Enabled at startup.")
                Else
                    Using k = entryLoc.Root.OpenSubKey(entryLoc.SubKey, True)
                        If k Is Nothing Then Return (False, "Registry key not available.")
                        Dim cmd = Convert.ToString(k.GetValue(entry.Name, ""))
                        If String.IsNullOrEmpty(cmd) Then Return (False, "Entry not found.")
                        Using bk = Registry.CurrentUser.CreateSubKey(BackupKey, True)
                            bk.SetValue(backupName, cmd, RegistryValueKind.String)
                        End Using
                        k.DeleteValue(entry.Name, False)
                    End Using
                    Logger.Info($"Startup disabled: {entry.Name}")
                    Return (True, "Disabled. AVAK kept a copy so you can re-enable it.")
                End If
            Catch ex As UnauthorizedAccessException
                Return (False, "Access denied. Restart AVAK as administrator.")
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

        Private Shared Function ToggleStartupFile(entry As StartupEntry, enable As Boolean) As (Ok As Boolean, Message As String)
            Try
                Dim src = entry.Command
                If enable Then
                    If Not src.EndsWith(".avak-disabled", StringComparison.OrdinalIgnoreCase) Then
                        Return (True, "Already enabled.")
                    End If
                    Dim dst = src.Substring(0, src.Length - ".avak-disabled".Length)
                    File.Move(src, dst)
                    Return (True, "Enabled at startup.")
                Else
                    If Not File.Exists(src) Then Return (False, "File not found.")
                    File.Move(src, src & ".avak-disabled")
                    Return (True, "Disabled (renamed, not deleted).")
                End If
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

        ' -- AVAK's own autostart --------------------------------------------

        Private Const SelfName As String = "AVAK"

        Public Shared Function IsSelfAutoStart() As Boolean
            Try
                Using k = Registry.CurrentUser.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", False)
                    Return k IsNot Nothing AndAlso k.GetValue(SelfName) IsNot Nothing
                End Using
            Catch
                Return False
            End Try
        End Function

        Public Shared Function SetSelfAutoStart(enable As Boolean) As Boolean
            Try
                Using k = Registry.CurrentUser.CreateSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", True)
                    If enable Then
                        k.SetValue(SelfName, """" & AppPaths.ExecutablePath & """ --tray", RegistryValueKind.String)
                    Else
                        k.DeleteValue(SelfName, False)
                    End If
                End Using
                Return True
            Catch ex As Exception
                Logger.Warn("SetSelfAutoStart failed: " & ex.Message)
                Return False
            End Try
        End Function

        Private Shared Function ExtractExe(command As String) As String
            If String.IsNullOrWhiteSpace(command) Then Return ""
            Dim s = command.Trim()
            Try
                If s.StartsWith("""") Then
                    Dim close_ = s.IndexOf(""""c, 1)
                    If close_ > 1 Then Return s.Substring(1, close_ - 1)
                End If
                Dim idx = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase)
                If idx > 0 Then Return s.Substring(0, idx + 4)
                Dim sp = s.IndexOf(" "c)
                Return If(sp > 0, s.Substring(0, sp), s)
            Catch
                Return s
            End Try
        End Function

    End Class

End Namespace
