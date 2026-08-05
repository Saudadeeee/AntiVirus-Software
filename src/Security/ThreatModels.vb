Imports System.Drawing
Imports AVAK.Theme

Namespace Security

    Public Enum Severity
        Clean = 0
        Low = 1
        Medium = 2
        High = 3
        Critical = 4
    End Enum

    Public Enum DetectionSource
        Signature = 0
        Heuristic = 1
        Pattern = 2
        Reputation = 3
    End Enum

    Public Enum ScanProfile
        Quick = 0
        Full = 1
        Custom = 2
        Memory = 3
        Realtime = 4
    End Enum

    ''' <summary>One finding produced by the scan engine.</summary>
    Public Class Detection
        Public Property FilePath As String = ""
        Public Property ThreatName As String = ""
        Public Property Severity As Severity = Severity.Medium
        Public Property Source As DetectionSource = DetectionSource.Signature
        Public Property Sha256 As String = ""
        Public Property SizeBytes As Long = 0
        Public Property DetectedAt As DateTime = DateTime.Now
        Public Property Reasons As New List(Of String)()
        Public Property Score As Integer = 0
        Public Property Quarantined As Boolean = False

        Public ReadOnly Property FileName As String
            Get
                Try
                    Return IO.Path.GetFileName(FilePath)
                Catch
                    Return FilePath
                End Try
            End Get
        End Property

        Public ReadOnly Property ReasonText As String
            Get
                Return If(Reasons Is Nothing OrElse Reasons.Count = 0, "-", String.Join("; ", Reasons))
            End Get
        End Property
    End Class

    Public NotInheritable Class SeverityUi
        Private Sub New()
        End Sub

        Public Shared Function Color_(s As Severity) As Color
            Select Case s
                Case Severity.Critical : Return ThemeManager.Colors.Red
                Case Severity.High : Return ThemeManager.Colors.Maroon
                Case Severity.Medium : Return ThemeManager.Colors.Peach
                Case Severity.Low : Return ThemeManager.Colors.Yellow
                Case Else : Return ThemeManager.Colors.Green
            End Select
        End Function

        Public Shared Function Icon(s As Severity) As String
            Select Case s
                Case Severity.Critical, Severity.High : Return "shield-alert"
                Case Severity.Medium : Return "alert"
                Case Severity.Low : Return "info"
                Case Else : Return "check-circle"
            End Select
        End Function

        Public Shared Function Label(s As Severity) As String
            Return s.ToString()
        End Function
    End Class

    ''' <summary>Live progress pushed to the UI while a scan runs.</summary>
    Public Class ScanProgressInfo
        Public Property FilesScanned As Long
        Public Property TotalFiles As Long
        Public Property BytesScanned As Long
        Public Property CurrentFile As String = ""
        Public Property ThreatsFound As Integer
        Public Property Elapsed As TimeSpan
        Public Property Phase As String = ""

        Public ReadOnly Property Percent As Single
            Get
                If TotalFiles <= 0 Then Return 0
                Return CSng(Math.Min(100.0R, FilesScanned * 100.0R / TotalFiles))
            End Get
        End Property
    End Class

    ''' <summary>Final result of one scan, also the unit stored in scan history.</summary>
    Public Class ScanReport
        Public Property Id As String = Guid.NewGuid().ToString("N").Substring(0, 12)
        Public Property Profile As ScanProfile = ScanProfile.Quick
        Public Property StartedAt As DateTime = DateTime.Now
        Public Property DurationMs As Long = 0
        Public Property FilesScanned As Long = 0
        Public Property BytesScanned As Long = 0
        Public Property SkippedFiles As Long = 0
        Public Property ErrorCount As Long = 0
        Public Property Cancelled As Boolean = False
        Public Property Roots As New List(Of String)()
        Public Property Detections As New List(Of Detection)()

        Public ReadOnly Property Duration As TimeSpan
            Get
                Return TimeSpan.FromMilliseconds(DurationMs)
            End Get
        End Property

        Public ReadOnly Property ThreatCount As Integer
            Get
                Return If(Detections Is Nothing, 0, Detections.Count)
            End Get
        End Property

        Public ReadOnly Property WorstSeverity As Severity
            Get
                If Detections Is Nothing OrElse Detections.Count = 0 Then Return Severity.Clean
                Return Detections.Max(Function(d) d.Severity)
            End Get
        End Property
    End Class

End Namespace
