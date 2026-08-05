Imports AVAK.Security

Namespace Engine

    ''' <summary>
    ''' The one interface you implement to teach AVAK a new way of detecting things.
    '''
    ''' Implement it, drop the assembly in the <c>plugins</c> folder next to AVAK.exe,
    ''' and the registry picks it up on the next start. See docs/EXTENDING.md.
    '''
    ''' Contract:
    '''   * <see cref="Inspect"/> may be called from many threads at once. Keep it
    '''     re-entrant; do not mutate shared state without a lock.
    '''   * Return Nothing for "clean". Returning a detection stops later providers
    '''     only if <see cref="Terminal"/> is True.
    '''   * Never throw. The registry catches exceptions and disables a provider that
    '''     keeps failing, but a provider that throws is a broken provider.
    '''   * Read from the context; do not open the file again unless you must.
    ''' </summary>
    Public Interface IDetectionProvider

        ''' <summary>Stable identifier, e.g. "avak.hash" or "acme.yara". Used in settings and logs.</summary>
        ReadOnly Property Id As String

        ''' <summary>Human name shown in the Rules page.</summary>
        ReadOnly Property DisplayName As String

        ''' <summary>One line explaining what this provider looks for.</summary>
        ReadOnly Property Description As String

        ''' <summary>
        ''' Lower runs first. Built-ins use 100 (hash), 200 (pattern), 300 (archive),
        ''' 900 (heuristic). Put cheap and certain checks early.
        ''' </summary>
        ReadOnly Property Order As Integer

        ''' <summary>
        ''' When True, a detection from this provider ends the scan of that file.
        ''' Use it for exact matches; leave it False for scoring providers so other
        ''' evidence can still be collected.
        ''' </summary>
        ReadOnly Property Terminal As Boolean

        ''' <summary>
        ''' Called once at startup. Load your rules here and return False to opt out
        ''' (for example when your rule file is missing) - the provider is then skipped
        ''' without an error.
        ''' </summary>
        Function Initialise() As Boolean

        ''' <summary>Cheap pre-filter. Return False to skip <see cref="Inspect"/> for this file.</summary>
        Function CanHandle(context As DetectionContext) As Boolean

        ''' <summary>The actual check. Return Nothing when nothing was found.</summary>
        Function Inspect(context As DetectionContext) As Detection

    End Interface

    ''' <summary>
    ''' Convenience base class: sensible defaults so a provider only has to override
    ''' what it cares about.
    ''' </summary>
    Public MustInherit Class DetectionProviderBase
        Implements IDetectionProvider

        Public MustOverride ReadOnly Property Id As String Implements IDetectionProvider.Id
        Public MustOverride ReadOnly Property DisplayName As String Implements IDetectionProvider.DisplayName

        Public Overridable ReadOnly Property Description As String Implements IDetectionProvider.Description
            Get
                Return ""
            End Get
        End Property

        Public Overridable ReadOnly Property Order As Integer Implements IDetectionProvider.Order
            Get
                Return 500
            End Get
        End Property

        Public Overridable ReadOnly Property Terminal As Boolean Implements IDetectionProvider.Terminal
            Get
                Return True
            End Get
        End Property

        Public Overridable Function Initialise() As Boolean Implements IDetectionProvider.Initialise
            Return True
        End Function

        Public Overridable Function CanHandle(context As DetectionContext) As Boolean Implements IDetectionProvider.CanHandle
            Return True
        End Function

        Public MustOverride Function Inspect(context As DetectionContext) As Detection Implements IDetectionProvider.Inspect

        ''' <summary>Helper so providers do not have to fill in the boilerplate fields.</summary>
        Protected Function Detect(context As DetectionContext, threatName As String, severity As Severity,
                               source As DetectionSource, score As Integer,
                               ParamArray reasons As String()) As Detection
            Return New Detection With {
                .FilePath = context.FilePath,
                .ThreatName = threatName,
                .Severity = severity,
                .Source = source,
                .Sha256 = context.Sha256,
                .SizeBytes = context.SizeBytes,
                .Score = score,
                .Reasons = reasons.Where(Function(r) Not String.IsNullOrWhiteSpace(r)).ToList()
            }
        End Function

    End Class

End Namespace
