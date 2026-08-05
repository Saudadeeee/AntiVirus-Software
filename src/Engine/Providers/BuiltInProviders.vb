Imports AVAK.Content
Imports AVAK.Core
Imports AVAK.Security

Namespace Engine.Providers

    ''' <summary>Exact SHA-256 / MD5 match against every enabled rule pack.</summary>
    Public Class HashProvider
        Inherits DetectionProviderBase

        Public Overrides ReadOnly Property Id As String
            Get
                Return "avak.hash"
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayName As String
            Get
                Return "Hash signatures"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Exact SHA-256 and MD5 matches from the rule packs"
            End Get
        End Property

        Public Overrides ReadOnly Property Order As Integer
            Get
                Return 100
            End Get
        End Property

        Public Overrides Function CanHandle(context As DetectionContext) As Boolean
            Return context.SizeBytes > 0
        End Function

        Public Overrides Function Inspect(context As DetectionContext) As Detection
            Dim sha = context.Sha256
            If sha.Length = 0 Then Return Nothing

            ' MD5 is only worth computing when a pack actually carries MD5 entries
            Dim md5 = If(RuleStore.HasMd5Signatures, context.Md5, "")
            Dim known = RuleStore.MatchHash(sha, md5)
            If known Is Nothing Then Return Nothing

            Return Detect(context, known.Signature.Name, known.Signature.Severity,
                       DetectionSource.Signature, 100,
                       "Exact signature match",
                       "Rule pack: " & known.PackName)
        End Function
    End Class

    ''' <summary>Byte and ASCII patterns, optionally scoped by extension and offset.</summary>
    Public Class PatternProvider
        Inherits DetectionProviderBase

        Public Overrides ReadOnly Property Id As String
            Get
                Return "avak.pattern"
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayName As String
            Get
                Return "Byte patterns"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Wildcard hex and ASCII patterns from the rule packs"
            End Get
        End Property

        Public Overrides ReadOnly Property Order As Integer
            Get
                Return 200
            End Get
        End Property

        Public Overrides Function CanHandle(context As DetectionContext) As Boolean
            Return context.BufferLength > 0
        End Function

        Public Overrides Function Inspect(context As DetectionContext) As Detection
            Dim hits = RuleStore.MatchPatterns(context.Buffer, context.BufferLength, context.Extension)
            If hits.Count = 0 Then Return Nothing

            Dim worst = hits.OrderByDescending(Function(p) p.Pattern.Severity).First()
            Return Detect(context, worst.Pattern.Name, worst.Pattern.Severity,
                       DetectionSource.Pattern, 95,
                       "Byte pattern: " & String.Join(", ", hits.Select(Function(p) p.Pattern.Name).Take(3)),
                       "Rule pack: " & worst.PackName)
        End Function
    End Class

    ''' <summary>Opens ZIP-family containers and scans every entry.</summary>
    Public Class ArchiveProvider
        Inherits DetectionProviderBase

        Public Overrides ReadOnly Property Id As String
            Get
                Return "avak.archive"
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayName As String
            Get
                Return "Archive inspection"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "Looks inside ZIP, JAR, APK and Office containers"
            End Get
        End Property

        Public Overrides ReadOnly Property Order As Integer
            Get
                Return 300
            End Get
        End Property

        Public Overrides Function CanHandle(context As DetectionContext) As Boolean
            If Not context.Settings.ScanArchives Then Return False
            Return ArchiveScanner.IsSupported(context.Extension) OrElse
                   ArchiveScanner.LooksLikeZip(context.Buffer, context.BufferLength)
        End Function

        Public Overrides Function Inspect(context As DetectionContext) As Detection
            Dim inner = ArchiveScanner.Scan(context.FilePath, context.Settings)
            If inner.Count = 0 Then Return Nothing

            Dim worst = inner.OrderByDescending(Function(x) x.Severity).First()
            Dim why As New List(Of String) From {
                $"{inner.Count} suspicious entr{If(inner.Count = 1, "y", "ies")} inside the archive"}
            For Each e In inner.Take(4)
                why.Add($"{e.EntryName}: {e.ThreatName}")
            Next

            Return New Detection With {
                .FilePath = context.FilePath,
                .ThreatName = worst.ThreatName,
                .Severity = worst.Severity,
                .Source = worst.Source,
                .Sha256 = context.Sha256,
                .SizeBytes = context.SizeBytes,
                .Score = 90,
                .Reasons = why}
        End Function
    End Class

    ''' <summary>Structural and behavioural scoring driven by content/heuristics.json.</summary>
    Public Class HeuristicProvider
        Inherits DetectionProviderBase

        Public Overrides ReadOnly Property Id As String
            Get
                Return "avak.heuristic"
            End Get
        End Property

        Public Overrides ReadOnly Property DisplayName As String
            Get
                Return "Heuristic analysis"
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "PE structure, entropy, risky imports, script obfuscation and filename lures"
            End Get
        End Property

        Public Overrides ReadOnly Property Order As Integer
            Get
                Return 900
            End Get
        End Property

        Public Overrides Function CanHandle(context As DetectionContext) As Boolean
            Return context.Settings.HeuristicsEnabled AndAlso context.BufferLength > 0
        End Function

        Public Overrides Function Inspect(context As DetectionContext) As Detection
            Dim h = HeuristicAnalyzer.Analyze(context.FilePath, context.Buffer, context.BufferLength,
                                              context.File, context.Settings.HeuristicSensitivity)
            If h.Severity = Severity.Clean OrElse h.Severity = Severity.Low Then Return Nothing

            Dim family = If(h.IsPortableExecutable, "Win32", "Script")
            Dim kind = If(h.MaxSectionEntropy >= 7.4, "Packed", "Suspicious")

            Return New Detection With {
                .FilePath = context.FilePath,
                .ThreatName = $"HEUR:{family}/{kind}.Gen",
                .Severity = h.Severity,
                .Source = DetectionSource.Heuristic,
                .Sha256 = context.Sha256,
                .SizeBytes = context.SizeBytes,
                .Score = h.Score,
                .Reasons = h.Reasons}
        End Function
    End Class

End Namespace
