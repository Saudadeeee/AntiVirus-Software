Imports System.IO
Imports AVAK.Core
Imports AVAK.Security

Namespace Content

    Public Class Thresholds
        Public Property Low As Integer = 18
        Public Property Medium As Integer = 32
        Public Property High As Integer = 50
        Public Property Critical As Integer = 70
    End Class

    Public Class SensitivityMultipliers
        Public Property Lenient As Double = 0.75
        Public Property Balanced As Double = 1.0
        Public Property Aggressive As Double = 1.25
    End Class

    Public Class ExtensionGroups
        Public Property Executable As New List(Of String)()
        Public Property Script As New List(Of String)()
        Public Property Document As New List(Of String)()
        Public Property Lure As New List(Of String)()
    End Class

    Public Class StructuralWeights
        Public Property DoubleExtension As Integer = 34
        Public Property RightToLeftOverride As Integer = 40
        Public Property ExecutableWithWrongExtension As Integer = 30
        Public Property WritableExecutableSection As Integer = 20
        Public Property PackerSectionNames As Integer = 24
        Public Property VeryHighEntropy As Integer = 20
        Public Property VeryHighEntropyAggressiveBonus As Integer = 6
        Public Property HighEntropy As Integer = 10
        Public Property NativeSubsystem As Integer = 8
        Public Property MalformedPeHeaders As Integer = 8
        Public Property UnusualSectionCount As Integer = 14
        Public Property OfficeMacroProject As Integer = 26
        Public Property OfficeAutoOpen As Integer = 22
        Public Property AutorunLaunches As Integer = 28
        Public Property StagedInTemp As Integer = 12
        Public Property HidingInRecycleBin As Integer = 26
        Public Property HiddenAndSystem As Integer = 16
        Public Property LongEncodedBlob As Integer = 22
        Public Property MediumEncodedBlob As Integer = 10
    End Class

    Public Class EntropyLimits
        Public Property VeryHigh As Double = 7.6
        Public Property High As Double = 7.2
        Public Property PackedLabel As Double = 7.4
    End Class

    Public Class EncodedBlobLimits
        Public Property LongRun As Integer = 3000
        Public Property MediumRun As Integer = 900
    End Class

    Public Class WeightedName
        Public Property Name As String = ""
        Public Property Weight As Integer = 0
    End Class

    Public Class ScriptMarker
        Public Property Needle As String = ""
        Public Property Weight As Integer = 0
        Public Property Why As String = ""
    End Class

    Public Class ApiScoring
        Public Property ManyHitsThreshold As Integer = 3
        Public Property ManyHitsCap As Integer = 46
        Public Property FewHitsMinimumWeight As Integer = 12
        Public Property FewHitsCap As Integer = 18
    End Class

    Public Class ScriptScoring
        Public Property Cap As Integer = 60
        Public Property MaxReasonsShown As Integer = 4
    End Class

    ''' <summary>
    ''' Every number the heuristic engine uses, loaded from JSON so it can be tuned
    ''' without touching code.
    '''
    ''' Lookup order (first hit wins):
    '''   %LOCALAPPDATA%\AVAK\heuristics.json     your override
    '''   &lt;app&gt;\content\heuristics.json      shipped default
    '''   compiled-in defaults                    if both are missing
    ''' </summary>
    Public Class HeuristicProfile

        Public Property Name As String = "built-in"
        Public Property Version As String = "1"
        Public Property Description As String = ""

        Public Property Thresholds As New Thresholds()
        Public Property SensitivityMultipliers As New SensitivityMultipliers()
        Public Property Extensions As New ExtensionGroups()
        Public Property Structural As New StructuralWeights()
        Public Property Entropy As New EntropyLimits()
        Public Property EncodedBlob As New EncodedBlobLimits()
        Public Property PackerSectionNames As New List(Of String)()
        Public Property RiskyApis As New List(Of WeightedName)()
        Public Property ApiScoring As New ApiScoring()
        Public Property ScriptMarkers As New List(Of ScriptMarker)()
        Public Property ScriptScoring As New ScriptScoring()

        ' -- fast lookup sets, built after loading -----------------------------
        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly ExecutableSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly ScriptSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly DocumentSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly LureSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly Property SourcePath As String
            Get
                Return _sourcePath
            End Get
        End Property

        Private _sourcePath As String = "(built-in defaults)"

        Public Sub Rebuild()
            If Extensions Is Nothing Then Extensions = New ExtensionGroups()
            If PackerSectionNames Is Nothing Then PackerSectionNames = New List(Of String)()
            If RiskyApis Is Nothing Then RiskyApis = New List(Of WeightedName)()
            If ScriptMarkers Is Nothing Then ScriptMarkers = New List(Of ScriptMarker)()

            Fill(ExecutableSet, Extensions.Executable, DefaultExecutable)
            Fill(ScriptSet, Extensions.Script, DefaultScript)
            Fill(DocumentSet, Extensions.Document, DefaultDocument)
            Fill(LureSet, Extensions.Lure, DefaultLure)
        End Sub

        Private Shared Sub Fill(target As HashSet(Of String), source As List(Of String), fallback As String())
            target.Clear()
            Dim items = If(source Is Nothing OrElse source.Count = 0, fallback.ToList(), source)
            For Each s In items
                If Not String.IsNullOrWhiteSpace(s) Then target.Add(s.Trim())
            Next
        End Sub

        Public Function SeverityFor(score As Integer) As Severity
            If score >= Thresholds.Critical Then Return Security.Severity.Critical
            If score >= Thresholds.High Then Return Security.Severity.High
            If score >= Thresholds.Medium Then Return Security.Severity.Medium
            If score >= Thresholds.Low Then Return Security.Severity.Low
            Return Security.Severity.Clean
        End Function

        Public Function MultiplierFor(sensitivity As Integer) As Double
            Select Case sensitivity
                Case 1 : Return SensitivityMultipliers.Lenient
                Case 3 : Return SensitivityMultipliers.Aggressive
                Case Else : Return SensitivityMultipliers.Balanced
            End Select
        End Function

        ' -- loading -----------------------------------------------------------

        Private Shared _current As HeuristicProfile
        Private Shared ReadOnly Gate As New Object()

        Public Shared Event Changed As EventHandler

        Public Shared ReadOnly Property Current As HeuristicProfile
            Get
                SyncLock Gate
                    If _current Is Nothing Then _current = Load()
                    Return _current
                End SyncLock
            End Get
        End Property

        Public Shared ReadOnly Property UserPath As String
            Get
                Return Path.Combine(AppPaths.Root, "heuristics.json")
            End Get
        End Property

        Public Shared ReadOnly Property BuiltInPath As String
            Get
                Return Path.Combine(AppPaths.AppDir, "content", "heuristics.json")
            End Get
        End Property

        Public Shared Function Load() As HeuristicProfile
            For Each candidate In {UserPath, BuiltInPath}
                Dim p = Json.Load(Of HeuristicProfile)(candidate)
                If p IsNot Nothing Then
                    p._sourcePath = candidate
                    p.Rebuild()
                    Logger.Info($"Heuristic profile '{p.Name}' loaded from {candidate}")
                    Return p
                End If
            Next

            Dim fallback As New HeuristicProfile()
            fallback.Rebuild()
            fallback.RiskyApis = DefaultApis()
            fallback.ScriptMarkers = DefaultMarkers()
            fallback.PackerSectionNames = DefaultPackers.ToList()
            Logger.Warn("No heuristics.json found - using compiled-in defaults")
            Return fallback
        End Function

        Public Shared Sub Reload()
            SyncLock Gate
                _current = Load()
            End SyncLock
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        ''' <summary>Writes the active profile to the user path so it can be edited.</summary>
        Public Shared Function CreateUserCopy() As String
            Dim p = Current
            Json.Save(UserPath, p)
            Reload()
            Return UserPath
        End Function

        ' -- compiled-in fallbacks --------------------------------------------

        Private Shared ReadOnly DefaultExecutable As String() =
            {".exe", ".dll", ".scr", ".com", ".sys", ".ocx", ".cpl", ".drv", ".efi"}

        Private Shared ReadOnly DefaultScript As String() =
            {".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".bat", ".cmd", ".hta", ".py", ".sh", ".reg"}

        Private Shared ReadOnly DefaultDocument As String() =
            {".doc", ".docm", ".xls", ".xlsm", ".ppt", ".pptm", ".docx", ".xlsx", ".pptx", ".rtf"}

        Private Shared ReadOnly DefaultLure As String() =
            {".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".txt", ".mp4", ".zip", ".rar"}

        Private Shared ReadOnly DefaultPackers As String() =
            {"UPX0", "UPX1", "UPX2", ".aspack", "ASPack", "Themida", ".vmp0", ".vmp1", "FSG!", "MPRESS1", "MEW"}

        Private Shared Function DefaultApis() As List(Of WeightedName)
            Return New List(Of WeightedName) From {
                New WeightedName With {.Name = "WriteProcessMemory", .Weight = 12},
                New WeightedName With {.Name = "CreateRemoteThread", .Weight = 14},
                New WeightedName With {.Name = "VirtualAllocEx", .Weight = 10},
                New WeightedName With {.Name = "SetWindowsHookEx", .Weight = 9},
                New WeightedName With {.Name = "URLDownloadToFile", .Weight = 13},
                New WeightedName With {.Name = "GetAsyncKeyState", .Weight = 11},
                New WeightedName With {.Name = "WinExec", .Weight = 8},
                New WeightedName With {.Name = "VirtualProtect", .Weight = 5}}
        End Function

        Private Shared Function DefaultMarkers() As List(Of ScriptMarker)
            Return New List(Of ScriptMarker) From {
                New ScriptMarker With {.Needle = "frombase64string", .Weight = 16, .Why = "base64 payload decode"},
                New ScriptMarker With {.Needle = "invoke-expression", .Weight = 18, .Why = "Invoke-Expression"},
                New ScriptMarker With {.Needle = "-encodedcommand", .Weight = 22, .Why = "encoded PowerShell command"},
                New ScriptMarker With {.Needle = "downloadstring", .Weight = 20, .Why = "remote script download"},
                New ScriptMarker With {.Needle = "vssadmin delete shadows", .Weight = 30, .Why = "deletes volume shadow copies"}}
        End Function

    End Class

End Namespace
