Imports Microsoft.Win32

Namespace Core

    ''' <summary>
    ''' "Scan with AVAK" in the Explorer right-click menu. Everything is written under
    ''' HKCU\Software\Classes, so no elevation is needed and uninstalling is a clean
    ''' key delete rather than a system-wide change.
    ''' </summary>
    Public NotInheritable Class ShellIntegration

        Private Sub New()
        End Sub

        Private Const VerbName As String = "AVAKScan"
        Private Const MenuText As String = "Scan with AVAK"

        Private Shared ReadOnly Targets As String() = {
            "*\shell\" & VerbName,                 ' any file
            "Directory\shell\" & VerbName,         ' any folder
            "Drive\shell\" & VerbName              ' a whole drive
        }

        Public Shared ReadOnly Property IsRegistered As Boolean
            Get
                Try
                    Using k = Registry.CurrentUser.OpenSubKey("Software\Classes\" & Targets(0))
                        Return k IsNot Nothing
                    End Using
                Catch
                    Return False
                End Try
            End Get
        End Property

        Public Shared Function Register() As (Ok As Boolean, Message As String)
            Try
                Dim exe = AppPaths.ExecutablePath
                For Each target In Targets
                    Using k = Registry.CurrentUser.CreateSubKey("Software\Classes\" & target, True)
                        k.SetValue("", MenuText, RegistryValueKind.String)
                        k.SetValue("Icon", """" & exe & """,0", RegistryValueKind.String)
                        Using cmd = k.CreateSubKey("command", True)
                            cmd.SetValue("", """" & exe & """ --scan-ui ""%1""", RegistryValueKind.String)
                        End Using
                    End Using
                Next
                Logger.Info("Explorer context menu registered")
                Return (True, "'Scan with AVAK' added to the Explorer right-click menu.")
            Catch ex As Exception
                Logger.Warn("Shell integration failed: " & ex.Message)
                Return (False, ex.Message)
            End Try
        End Function

        Public Shared Function Unregister() As (Ok As Boolean, Message As String)
            Try
                For Each target In Targets
                    Try
                        Registry.CurrentUser.DeleteSubKeyTree("Software\Classes\" & target, False)
                    Catch
                    End Try
                Next
                Logger.Info("Explorer context menu removed")
                Return (True, "Removed from the Explorer right-click menu.")
            Catch ex As Exception
                Return (False, ex.Message)
            End Try
        End Function

    End Class

End Namespace
