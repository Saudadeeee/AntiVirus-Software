Imports System.IO
Imports AVAK.Theme

Namespace Core

    Public Enum ScheduleMode
        Off = 0
        Daily = 1
        Weekly = 2
    End Enum

    Public Enum ThreatAction
        AskMe = 0
        Quarantine = 1
        ReportOnly = 2
    End Enum

    ''' <summary>User-visible configuration, persisted as JSON. Plain properties only.</summary>
    Public Class AppSettings

        ' -- appearance
        Public Property Flavor As Flavor = Flavor.Mocha
        Public Property AccentName As String = "Mauve"
        Public Property CornerRadius As Integer = 12
        Public Property Animations As Boolean = True

        ' -- protection
        Public Property RealtimeProtection As Boolean = False
        Public Property WatchedFolders As New List(Of String)()
        Public Property OnThreatFound As ThreatAction = ThreatAction.AskMe
        Public Property ScanArchives As Boolean = False
        Public Property HeuristicsEnabled As Boolean = True
        Public Property HeuristicSensitivity As Integer = 2      ' 1 = low, 2 = balanced, 3 = aggressive
        Public Property MaxFileSizeMb As Integer = 256
        Public Property ScanThreads As Integer = 0               ' 0 = auto (CPU count - 1)
        Public Property ExcludedPaths As New List(Of String)()
        Public Property ExcludedExtensions As New List(Of String)()
        ''' <summary>Ids of detection providers the user switched off on the Rules page.</summary>
        Public Property DisabledProviders As New List(Of String)()
        ''' <summary>Rule pack file names the user switched off.</summary>
        Public Property DisabledRulePacks As New List(Of String)()

        ' -- scheduler
        Public Property Schedule As ScheduleMode = ScheduleMode.Off
        Public Property ScheduleTime As String = "20:00"
        Public Property ScheduleDayOfWeek As Integer = 6         ' 0 = Sunday
        Public Property ScheduleQuickScan As Boolean = True
        Public Property LastScheduledRun As DateTime = DateTime.MinValue

        ' -- behaviour
        Public Property StartWithWindows As Boolean = False
        Public Property StartMinimized As Boolean = False
        Public Property MinimizeToTray As Boolean = True
        Public Property CloseToTray As Boolean = True
        Public Property ShowToasts As Boolean = True
        Public Property PlaySounds As Boolean = False
        Public Property ConfirmBeforeDelete As Boolean = True

        ' -- update feeds (empty = AVAK never contacts the network on its own)
        Public Property UpdateFeedUrl As String = ""
        Public Property SignatureFeedUrl As String = ""
        Public Property CheckUpdatesOnStart As Boolean = False
        Public Property LastUpdateCheckUtc As DateTime = DateTime.MinValue

        ' -- telemetry / stats (written by the app, shown on the dashboard)
        Public Property LastScanUtc As DateTime = DateTime.MinValue
        Public Property LastScanFiles As Long = 0
        Public Property LastScanThreats As Integer = 0
        Public Property TotalScans As Long = 0
        Public Property TotalThreatsBlocked As Long = 0

        ' -- runtime singleton ------------------------------------------------
        Private Shared _current As AppSettings

        Public Shared ReadOnly Property Current As AppSettings
            Get
                If _current Is Nothing Then _current = Load()
                Return _current
            End Get
        End Property

        Public Shared Function Load() As AppSettings
            Dim s = Json.Load(Of AppSettings)(AppPaths.SettingsFile)
            If s Is Nothing Then
                s = New AppSettings()
                s.WatchedFolders.AddRange(DefaultWatchedFolders())
                s.ExcludedExtensions.AddRange({".log", ".tmp", ".etl", ".evtx"})
                Json.Save(AppPaths.SettingsFile, s)
            End If
            s.Normalize()
            _current = s
            Return s
        End Function

        Public Sub Save()
            Normalize()
            Json.Save(AppPaths.SettingsFile, Me)
        End Sub

        Private Sub Normalize()
            If WatchedFolders Is Nothing Then WatchedFolders = New List(Of String)()
            If ExcludedPaths Is Nothing Then ExcludedPaths = New List(Of String)()
            If ExcludedExtensions Is Nothing Then ExcludedExtensions = New List(Of String)()
            If DisabledProviders Is Nothing Then DisabledProviders = New List(Of String)()
            If DisabledRulePacks Is Nothing Then DisabledRulePacks = New List(Of String)()
            If String.IsNullOrWhiteSpace(AccentName) Then AccentName = "Mauve"
            CornerRadius = Math.Max(0, Math.Min(20, CornerRadius))
            HeuristicSensitivity = Math.Max(1, Math.Min(3, HeuristicSensitivity))
            MaxFileSizeMb = Math.Max(1, Math.Min(4096, MaxFileSizeMb))
            ScanThreads = Math.Max(0, Math.Min(64, ScanThreads))
        End Sub

        Public Function EffectiveThreads() As Integer
            If ScanThreads > 0 Then Return ScanThreads
            Return Math.Max(1, Environment.ProcessorCount - 1)
        End Function

        Public Sub ApplyTheme()
            ThemeManager.Apply(Flavor, AccentName, CornerRadius)
        End Sub

        Public Shared Function DefaultWatchedFolders() As List(Of String)
            Dim l As New List(Of String)()
            For Each sf In {Environment.SpecialFolder.UserProfile,
                            Environment.SpecialFolder.MyDocuments,
                            Environment.SpecialFolder.Desktop}
                Try
                    Dim p = Environment.GetFolderPath(sf)
                    If Not String.IsNullOrEmpty(p) AndAlso Directory.Exists(p) Then l.Add(p)
                Catch
                End Try
            Next
            Try
                Dim dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                If Directory.Exists(dl) Then l.Insert(0, dl)
            Catch
            End Try
            Return l.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        End Function

    End Class

End Namespace
