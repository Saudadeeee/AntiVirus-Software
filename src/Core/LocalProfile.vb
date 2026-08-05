Imports System.Security.Cryptography
Imports System.Text

Namespace Core

    ''' <summary>
    ''' A purely local profile - no account server, no telemetry, nothing leaves the PC.
    ''' The optional PIN is stored as a PBKDF2-SHA256 hash so the plain value never
    ''' touches disk; it gates the app window, not the file system.
    ''' </summary>
    Public Class LocalProfile

        Public Property DisplayName As String = Environment.UserName
        Public Property AvatarAccent As String = "Mauve"
        Public Property CreatedAt As DateTime = DateTime.Now
        Public Property LastOpenedAt As DateTime = DateTime.Now
        Public Property Launches As Long = 0

        ''' <summary>Base64 salt + hash. Empty means no PIN is set.</summary>
        Public Property PinSalt As String = ""
        Public Property PinHash As String = ""
        Public Property LockOnStart As Boolean = False

        Private Const Iterations As Integer = 120000

        Private Shared _current As LocalProfile

        Public Shared ReadOnly Property Current As LocalProfile
            Get
                If _current Is Nothing Then
                    _current = Json.Load(Of LocalProfile)(AppPaths.ProfileFile)
                    If _current Is Nothing Then
                        _current = New LocalProfile()
                        _current.Save()
                    End If
                End If
                Return _current
            End Get
        End Property

        Public Sub Save()
            Json.Save(AppPaths.ProfileFile, Me)
        End Sub

        Public ReadOnly Property HasPin As Boolean
            Get
                Return Not String.IsNullOrEmpty(PinHash)
            End Get
        End Property

        Public Sub SetPin(pin As String)
            If String.IsNullOrEmpty(pin) Then
                PinSalt = ""
                PinHash = ""
                LockOnStart = False
                Save()
                Return
            End If
            Dim salt(15) As Byte
            RandomNumberGenerator.Fill(salt)
            PinSalt = Convert.ToBase64String(salt)
            PinHash = Convert.ToBase64String(Derive(pin, salt))
            Save()
        End Sub

        Public Function VerifyPin(pin As String) As Boolean
            If Not HasPin Then Return True
            Try
                Dim salt = Convert.FromBase64String(PinSalt)
                Dim expected = Convert.FromBase64String(PinHash)
                Dim actual = Derive(pin, salt)
                Return CryptographicOperations.FixedTimeEquals(expected, actual)
            Catch
                Return False
            End Try
        End Function

        Private Shared Function Derive(pin As String, salt As Byte()) As Byte()
            Using kdf As New Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256)
                Return kdf.GetBytes(32)
            End Using
        End Function

        Public Sub NoteLaunch()
            Launches += 1
            LastOpenedAt = DateTime.Now
            Save()
        End Sub

        Public Function Initials() As String
            Dim n = If(String.IsNullOrWhiteSpace(DisplayName), "A", DisplayName.Trim())
            Dim parts = n.Split(New Char() {" "c, "."c, "_"c, "-"c}, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length >= 2 Then Return (parts(0)(0).ToString() & parts(1)(0).ToString()).ToUpperInvariant()
            Return n.Substring(0, Math.Min(2, n.Length)).ToUpperInvariant()
        End Function

    End Class

End Namespace
