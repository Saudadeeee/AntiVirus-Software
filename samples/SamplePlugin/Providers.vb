Imports System.IO
Imports System.Text
Imports AVAK.Engine
Imports AVAK.Security

Namespace SamplePlugin

    ''' <summary>
    ''' Example 1 - the smallest useful provider.
    '''
    ''' Flags files whose name contains a common lure word AND which are executable.
    ''' Real-world value is limited; the point is to show the shape of a provider:
    ''' declare an id, decide cheaply in CanHandle, then answer in Inspect.
    ''' </summary>
    Public Class LureNameProvider
        Inherits DetectionProviderBase

        Private Shared ReadOnly Lures As String() = {
            "invoice", "receipt", "payment", "urgent", "scan_", "dhl", "fedex",
            "resume", "cv_", "salary", "bonus", "refund", "statement"}

        Public Overrides ReadOnly Property Id As String
            Get
                Return "sample.lurename"
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayName As String
            Get
                Return "Lure filenames (sample)"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Executables named like a phishing attachment"
            End Get
        End Property

        Public Overrides ReadOnly Property Order As Integer
            Get
                Return 850          ' just before the built-in heuristics
            End Get
        End Property

        ''' <summary>Not terminal: let the built-in heuristics have a look as well.</summary>
        Public Overrides ReadOnly Property Terminal As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function CanHandle(context As DetectionContext) As Boolean
            ' Cheap test first - never read the file here.
            Return context.HasExtension(".exe", ".scr", ".com", ".bat", ".cmd", ".js", ".vbs", ".lnk")
        End Function

        Public Overrides Function Inspect(context As DetectionContext) As Detection
            Dim name = context.FileName.ToLowerInvariant()
            Dim lure = Lures.FirstOrDefault(Function(l) name.Contains(l))
            If lure Is Nothing Then Return Nothing

            Return Detect(context, "Sample:Lure/Filename", Severity.Medium,
                          DetectionSource.Heuristic, 40,
                          $"Filename contains the lure word '{lure}'",
                          "Executable delivered under a document-like name")
        End Function
    End Class

    ''' <summary>
    ''' Example 2 - a provider that reads content and keeps its own state.
    '''
    ''' Loads a word list from plugins\blocked-words.txt (one per line) at startup
    ''' and reports any script containing one of them. Shows Initialise() opting out
    ''' when its data file is missing, and safe use of the shared read-only buffer.
    ''' </summary>
    Public Class WordListProvider
        Inherits DetectionProviderBase

        Private _words As String() = Array.Empty(Of String)()

        Public Overrides ReadOnly Property Id As String
            Get
                Return "sample.wordlist"
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayName As String
            Get
                Return "Blocked words (sample)"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Scripts containing a word from plugins\blocked-words.txt"
            End Get
        End Property

        Public Overrides ReadOnly Property Order As Integer
            Get
                Return 250
            End Get
        End Property

        Public Overrides Function Initialise() As Boolean
            Try
                Dim file = Path.Combine(ProviderRegistry.PluginFolder, "blocked-words.txt")
                If Not IO.File.Exists(file) Then Return False      ' opt out quietly

                _words = IO.File.ReadAllLines(file).
                    Select(Function(l) l.Trim().ToLowerInvariant()).
                    Where(Function(l) l.Length >= 4 AndAlso Not l.StartsWith("#")).
                    Distinct().ToArray()

                Return _words.Length > 0
            Catch
                Return False
            End Try
        End Function

        Public Overrides Function CanHandle(context As DetectionContext) As Boolean
            Return context.BufferLength > 0 AndAlso
                   context.HasExtension(".ps1", ".bat", ".cmd", ".vbs", ".js", ".hta", ".py", ".sh")
        End Function

        Public Overrides Function Inspect(context As DetectionContext) As Detection
            ' Latin1 keeps a byte-for-character mapping, so offsets stay meaningful.
            Dim text = Encoding.Latin1.GetString(context.Buffer, 0,
                                                 Math.Min(context.BufferLength, 512 * 1024)).ToLowerInvariant()

            Dim found = _words.Where(Function(w) text.Contains(w)).Take(4).ToArray()
            If found.Length = 0 Then Return Nothing

            Return Detect(context, "Sample:WordList/Match", Severity.High,
                       DetectionSource.Pattern, 70,
                       "Contains blocked word(s): " & String.Join(", ", found),
                       $"Word list: {_words.Length} entries")
        End Function
    End Class

End Namespace
