Imports System.IO
Imports AVAK.Core
Imports AVAK.Security

Namespace Engine

    ''' <summary>
    ''' Everything a detection provider is given about one file, plus lazily
    ''' computed values it can ask for. Nothing here is computed until a provider
    ''' actually reads it, so a provider that only needs the extension costs nothing.
    '''
    ''' A context is used by one thread at a time; the scan engine creates one per file.
    ''' </summary>
    Public NotInheritable Class DetectionContext

        Public Const WindowBytes As Integer = 2 * 1024 * 1024

        Public ReadOnly Property FilePath As String
        Public ReadOnly Property File As FileInfo
        Public ReadOnly Property Settings As AppSettings
        ''' <summary>Where the file came from - useful to providers that behave differently in real time.</summary>
        Public ReadOnly Property Trigger As ScanProfile

        Private _ext As String
        Private _name As String
        Private _sha256 As String
        Private _md5 As String
        Private _buffer As Byte()
        Private _bufferLength As Integer = -1

        Public Sub New(filePath As String, file As FileInfo, settings As AppSettings,
                       Optional trigger As ScanProfile = ScanProfile.Custom)
            _FilePath = filePath
            _File = file
            _Settings = settings
            _Trigger = trigger
        End Sub

        ''' <summary>Lower-case extension including the dot, or "" when there is none.</summary>
        Public ReadOnly Property Extension As String
            Get
                If _ext Is Nothing Then
                    Try
                        _ext = Path.GetExtension(FilePath)
                    Catch
                        _ext = ""
                    End Try
                End If
                Return _ext
            End Get
        End Property

        Public ReadOnly Property FileName As String
            Get
                If _name Is Nothing Then
                    Try
                        _name = Path.GetFileName(FilePath)
                    Catch
                        _name = FilePath
                    End Try
                End If
                Return _name
            End Get
        End Property

        Public ReadOnly Property SizeBytes As Long
            Get
                Return If(File Is Nothing, 0L, File.Length)
            End Get
        End Property

        ''' <summary>SHA-256 of the whole file, computed once. "" when unreadable.</summary>
        Public ReadOnly Property Sha256 As String
            Get
                If _sha256 Is Nothing Then _sha256 = SignatureDatabase.Sha256File(FilePath)
                Return _sha256
            End Get
        End Property

        ''' <summary>MD5 of the whole file. Only compute this if a provider needs it.</summary>
        Public ReadOnly Property Md5 As String
            Get
                If _md5 Is Nothing Then _md5 = SignatureDatabase.Md5File(FilePath)
                Return _md5
            End Get
        End Property

        ''' <summary>First <see cref="WindowBytes"/> of the file. Nothing when unreadable.</summary>
        Public ReadOnly Property Buffer As Byte()
            Get
                EnsureBuffer()
                Return _buffer
            End Get
        End Property

        ''' <summary>How many bytes of <see cref="Buffer"/> are valid.</summary>
        Public ReadOnly Property BufferLength As Integer
            Get
                EnsureBuffer()
                Return Math.Max(0, _bufferLength)
            End Get
        End Property

        Private Sub EnsureBuffer()
            If _bufferLength >= 0 Then Return
            Dim w = ScanEngine.ReadWindow(FilePath, SizeBytes)
            _buffer = w.Data
            _bufferLength = If(w.Data Is Nothing, 0, w.Length)
        End Sub

        ''' <summary>True when the window starts with the MZ signature.</summary>
        Public ReadOnly Property IsPortableExecutable As Boolean
            Get
                Return BufferLength > 64 AndAlso Buffer(0) = &H4D AndAlso Buffer(1) = &H5A
            End Get
        End Property

        Public Function HasExtension(ParamArray candidates As String()) As Boolean
            For Each c In candidates
                If String.Equals(Extension, c, StringComparison.OrdinalIgnoreCase) Then Return True
            Next
            Return False
        End Function

    End Class

End Namespace
