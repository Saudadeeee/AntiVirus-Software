Imports System.IO

Namespace Core

    ''' <summary>
    ''' Every file AVAK writes lives under %LOCALAPPDATA%\AVAK so the app stays
    ''' portable and never needs write access to Program Files.
    ''' </summary>
    Public NotInheritable Class AppPaths

        Private Sub New()
        End Sub

        Public Const AppName As String = "AVAK"

        Public Shared ReadOnly Property Root As String
            Get
                Return Ensure(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName))
            End Get
        End Property

        Public Shared ReadOnly Property Quarantine As String
            Get
                Return Ensure(Path.Combine(Root, "quarantine"))
            End Get
        End Property

        Public Shared ReadOnly Property Logs As String
            Get
                Return Ensure(Path.Combine(Root, "logs"))
            End Get
        End Property

        Public Shared ReadOnly Property Data As String
            Get
                Return Ensure(Path.Combine(Root, "data"))
            End Get
        End Property

        Public Shared ReadOnly Property SettingsFile As String
            Get
                Return Path.Combine(Root, "settings.json")
            End Get
        End Property

        Public Shared ReadOnly Property ProfileFile As String
            Get
                Return Path.Combine(Root, "profile.json")
            End Get
        End Property

        Public Shared ReadOnly Property HistoryFile As String
            Get
                Return Path.Combine(Data, "scan-history.json")
            End Get
        End Property

        Public Shared ReadOnly Property QuarantineIndex As String
            Get
                Return Path.Combine(Data, "quarantine-index.json")
            End Get
        End Property

        Public Shared ReadOnly Property EventsFile As String
            Get
                Return Path.Combine(Data, "events.json")
            End Get
        End Property

        ''' <summary>Drop file a second instance uses to hand a path to the running one.</summary>
        Public Shared ReadOnly Property ScanRequestFile As String
            Get
                Return Path.Combine(Data, "scan-request.txt")
            End Get
        End Property

        Public Shared ReadOnly Property UserSignatures As String
            Get
                Return Path.Combine(Data, "signatures.json")
            End Get
        End Property

        ''' <summary>signatures.json shipped next to the executable (read-only baseline).</summary>
        Public Shared ReadOnly Property BundledSignatures As String
            Get
                Return Path.Combine(AppDir, "data", "signatures.json")
            End Get
        End Property

        Public Shared ReadOnly Property AppDir As String
            Get
                Return AppDomain.CurrentDomain.BaseDirectory
            End Get
        End Property

        Public Shared ReadOnly Property ExecutablePath As String
            Get
                Dim p = Process.GetCurrentProcess().MainModule?.FileName
                Return If(String.IsNullOrEmpty(p), Path.Combine(AppDir, "AVAK.exe"), p)
            End Get
        End Property

        Private Shared Function Ensure(p As String) As String
            Try
                If Not Directory.Exists(p) Then Directory.CreateDirectory(p)
            Catch
            End Try
            Return p
        End Function

    End Class

End Namespace
