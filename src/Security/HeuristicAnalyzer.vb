Imports System.IO
Imports System.Text
Imports AVAK.Content
Imports AVAK.Core

Namespace Security

    Public Class HeuristicResult
        Public Property Score As Integer = 0
        Public Property Reasons As New List(Of String)()
        Public Property IsPortableExecutable As Boolean = False
        Public Property MaxSectionEntropy As Double = 0

        ''' <summary>Set by the analyser so severity uses the profile's thresholds.</summary>
        Friend Profile As HeuristicProfile

        Public ReadOnly Property Severity As Severity
            Get
                Return If(Profile, HeuristicProfile.Current).SeverityFor(Score)
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Static heuristics: PE structure, section entropy, suspicious imports,
    ''' script obfuscation markers, extension/magic mismatch and lure filenames.
    ''' Nothing here executes the sample - it is pure static inspection.
    '''
    ''' Every weight, threshold, extension list, API name and script marker comes
    ''' from <see cref="HeuristicProfile"/> (content/heuristics.json), so the engine
    ''' can be retuned without recompiling.
    ''' </summary>
    Public NotInheritable Class HeuristicAnalyzer

        Private Sub New()
        End Sub

        Public Shared Function Analyze(filePath As String, buffer As Byte(), length As Integer,
                                       info As FileInfo, sensitivity As Integer) As HeuristicResult
            Return Analyze(filePath, buffer, length, info, sensitivity, HeuristicProfile.Current)
        End Function

        ''' <summary>Overload that lets a caller (or a test) supply its own profile.</summary>
        Public Shared Function Analyze(filePath As String, buffer As Byte(), length As Integer,
                                       info As FileInfo, sensitivity As Integer,
                                       profile As HeuristicProfile) As HeuristicResult
            Dim res As New HeuristicResult With {.Profile = profile}
            If buffer Is Nothing OrElse length <= 0 Then Return res

            Dim w = profile.Structural

            Dim ext = ""
            Try
                ext = Path.GetExtension(filePath)
            Catch
            End Try
            Dim name = ""
            Try
                name = Path.GetFileName(filePath)
            Catch
            End Try

            Dim isPe = length > 64 AndAlso buffer(0) = &H4D AndAlso buffer(1) = &H5A
            res.IsPortableExecutable = isPe

            ' 1. double extension lure -------------------------------------------------
            If profile.ExecutableSet.Contains(ext) OrElse profile.ScriptSet.Contains(ext) Then
                Dim stem = Path.GetFileNameWithoutExtension(name)
                Dim inner = Path.GetExtension(stem)
                If Not String.IsNullOrEmpty(inner) AndAlso profile.LureSet.Contains(inner) Then
                    res.Score += w.DoubleExtension
                    res.Reasons.Add($"Double extension '{inner}{ext}' disguises an executable")
                End If
                ' right-to-left override, the classic filename spoof
                If name.Contains(ChrW(&H202E)) Then
                    res.Score += w.RightToLeftOverride
                    res.Reasons.Add("Filename contains a right-to-left override character")
                End If
            End If

            ' 2. magic vs extension ----------------------------------------------------
            If isPe AndAlso Not profile.ExecutableSet.Contains(ext) AndAlso Not String.IsNullOrEmpty(ext) Then
                res.Score += w.ExecutableWithWrongExtension
                res.Reasons.Add($"File is a Windows executable but named '{ext}'")
            End If

            ' 3. PE analysis -----------------------------------------------------------
            If isPe Then
                AnalyzePe(buffer, length, res, sensitivity, profile)
                AnalyzeApis(buffer, length, res, profile)
            End If

            ' 4. scripts ---------------------------------------------------------------
            If profile.ScriptSet.Contains(ext) OrElse (Not isPe AndAlso LooksTextual(buffer, Math.Min(length, 4096))) Then
                AnalyzeScript(buffer, length, res, profile)
            End If

            ' 5. Office macro containers -----------------------------------------------
            If profile.DocumentSet.Contains(ext) Then
                Dim txt = Latin1(buffer, Math.Min(length, 1024 * 512))
                If txt.IndexOf("vbaProject.bin", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    res.Score += w.OfficeMacroProject
                    res.Reasons.Add("Office document contains a VBA macro project")
                End If
                If txt.IndexOf("Auto_Open", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                   txt.IndexOf("AutoOpen", StringComparison.Ordinal) >= 0 OrElse
                   txt.IndexOf("Document_Open", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    res.Score += w.OfficeAutoOpen
                    res.Reasons.Add("Macro runs automatically when the document opens")
                End If
            End If

            ' 6. autorun ---------------------------------------------------------------
            If String.Equals(name, "autorun.inf", StringComparison.OrdinalIgnoreCase) Then
                Dim txt = Latin1(buffer, Math.Min(length, 8192)).ToLowerInvariant()
                If txt.Contains("open=") OrElse txt.Contains("shellexecute=") Then
                    res.Score += w.AutorunLaunches
                    res.Reasons.Add("autorun.inf launches a program automatically")
                End If
            End If

            ' 7. location -------------------------------------------------------------
            If info IsNot Nothing AndAlso (profile.ExecutableSet.Contains(ext) OrElse profile.ScriptSet.Contains(ext)) Then
                Dim dir = ""
                Try
                    dir = If(info.DirectoryName, "").ToLowerInvariant()
                Catch
                End Try
                If dir.Contains("\temp\") OrElse dir.EndsWith("\temp") OrElse dir.Contains("\appdata\local\temp") Then
                    res.Score += w.StagedInTemp
                    res.Reasons.Add("Executable staged in a temp folder")
                End If
                If dir.Contains("\recycle") Then
                    res.Score += w.HidingInRecycleBin
                    res.Reasons.Add("Executable hiding inside the recycle bin")
                End If
                Try
                    If (info.Attributes And FileAttributes.Hidden) = FileAttributes.Hidden AndAlso
                       (info.Attributes And FileAttributes.System) = FileAttributes.System Then
                        res.Score += w.HiddenAndSystem
                        res.Reasons.Add("Marked hidden + system")
                    End If
                Catch
                End Try
            End If

            res.Score = CInt(res.Score * profile.MultiplierFor(sensitivity))
            Return res
        End Function

        ' -- PE ------------------------------------------------------------------

        Private Shared Sub AnalyzePe(buf As Byte(), length As Integer, res As HeuristicResult,
                                     sensitivity As Integer, profile As HeuristicProfile)
            Dim w = profile.Structural
            Try
                Dim peOff = BitConverter.ToInt32(buf, &H3C)
                If peOff <= 0 OrElse peOff + 248 >= length Then Return
                If Not (buf(peOff) = &H50 AndAlso buf(peOff + 1) = &H45) Then Return   ' "PE"

                Dim numSections = BitConverter.ToUInt16(buf, peOff + 6)
                Dim optSize = BitConverter.ToUInt16(buf, peOff + 20)
                Dim characteristics = BitConverter.ToUInt16(buf, peOff + 22)
                Dim subsystem = BitConverter.ToUInt16(buf, peOff + 24 + 68)

                Dim sectionTable = peOff + 24 + optSize
                If numSections = 0 OrElse numSections > 96 Then
                    res.Score += w.UnusualSectionCount
                    res.Reasons.Add($"Unusual PE section count ({numSections})")
                End If

                Dim writableExec = 0
                Dim oddNames As New List(Of String)()

                For i = 0 To CInt(numSections) - 1
                    Dim so = sectionTable + i * 40
                    If so + 40 > length Then Exit For

                    Dim nameBytes = New Byte(7) {}
                    Array.Copy(buf, so, nameBytes, 0, 8)
                    Dim sName = Encoding.ASCII.GetString(nameBytes).TrimEnd(ChrW(0))

                    Dim rawSize = BitConverter.ToInt32(buf, so + 16)
                    Dim rawPtr = BitConverter.ToInt32(buf, so + 20)
                    Dim flags = BitConverter.ToUInt32(buf, so + 36)

                    If (flags And &H20000000UI) <> 0 AndAlso (flags And &H80000000UI) <> 0 Then writableExec += 1
                    If profile.PackerSectionNames.Any(
                        Function(k) sName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) Then
                        oddNames.Add(sName)
                    End If

                    If rawSize > 0 AndAlso rawPtr > 0 AndAlso rawPtr + rawSize <= length Then
                        Dim ent = Entropy(buf, rawPtr, Math.Min(rawSize, 512 * 1024))
                        If ent > res.MaxSectionEntropy Then res.MaxSectionEntropy = ent
                    End If
                Next

                If writableExec > 0 Then
                    res.Score += w.WritableExecutableSection
                    res.Reasons.Add("PE has a writable + executable section")
                End If
                If oddNames.Count > 0 Then
                    res.Score += w.PackerSectionNames
                    res.Reasons.Add("Packer sections detected (" & String.Join(", ", oddNames.Distinct()) & ")")
                End If
                If res.MaxSectionEntropy >= profile.Entropy.VeryHigh Then
                    res.Score += w.VeryHighEntropy + If(sensitivity >= 3, w.VeryHighEntropyAggressiveBonus, 0)
                    res.Reasons.Add($"Very high section entropy ({res.MaxSectionEntropy:0.00}/8) - packed or encrypted")
                ElseIf res.MaxSectionEntropy >= profile.Entropy.High Then
                    res.Score += w.HighEntropy
                    res.Reasons.Add($"High section entropy ({res.MaxSectionEntropy:0.00}/8)")
                End If

                ' subsystem 1 = native (driver-like)
                If subsystem = 1 Then
                    res.Score += w.NativeSubsystem
                    res.Reasons.Add("Native subsystem binary (driver-like)")
                End If
                If (characteristics And &H2000US) <> 0 Then
                    res.Reasons.Add("Dynamic-link library")
                End If
            Catch
                res.Score += w.MalformedPeHeaders
                res.Reasons.Add("Malformed PE headers")
            End Try
        End Sub

        Private Shared Sub AnalyzeApis(buf As Byte(), length As Integer, res As HeuristicResult,
                                       profile As HeuristicProfile)
            If profile.RiskyApis.Count = 0 Then Return

            Dim text = Latin1(buf, Math.Min(length, 2 * 1024 * 1024))
            Dim hits As New List(Of String)()
            Dim weight = 0
            For Each api In profile.RiskyApis
                If String.IsNullOrEmpty(api.Name) Then Continue For
                If text.IndexOf(api.Name, StringComparison.Ordinal) >= 0 Then
                    hits.Add(api.Name)
                    weight += api.Weight
                End If
            Next

            Dim s = profile.ApiScoring
            If hits.Count >= s.ManyHitsThreshold Then
                res.Score += Math.Min(s.ManyHitsCap, weight)
                res.Reasons.Add("Imports high-risk APIs: " & String.Join(", ", hits.Take(5)) &
                                If(hits.Count > 5, $" (+{hits.Count - 5} more)", ""))
            ElseIf hits.Count > 0 AndAlso weight >= s.FewHitsMinimumWeight Then
                res.Score += Math.Min(s.FewHitsCap, weight)
                res.Reasons.Add("Imports risky APIs: " & String.Join(", ", hits))
            End If
        End Sub

        ' -- scripts --------------------------------------------------------------

        Private Shared Sub AnalyzeScript(buf As Byte(), length As Integer, res As HeuristicResult,
                                         profile As HeuristicProfile)
            Dim text = Latin1(buf, Math.Min(length, 512 * 1024)).ToLowerInvariant()
            Dim hits As New List(Of String)()
            Dim weight = 0
            For Each m In profile.ScriptMarkers
                If String.IsNullOrEmpty(m.Needle) Then Continue For
                If text.Contains(m.Needle) Then
                    hits.Add(If(String.IsNullOrWhiteSpace(m.Why), m.Needle, m.Why))
                    weight += m.Weight
                End If
            Next
            If hits.Count > 0 Then
                res.Score += Math.Min(profile.ScriptScoring.Cap, weight)
                res.Reasons.Add("Script behaviour: " &
                    String.Join(", ", hits.Distinct().Take(profile.ScriptScoring.MaxReasonsShown)))
            End If

            ' very long single-token blobs are a classic obfuscation tell
            Dim longestRun = 0, run = 0
            For i = 0 To Math.Min(length, 200000) - 1
                Dim c = ChrW(buf(i))
                If Char.IsLetterOrDigit(c) OrElse c = "+"c OrElse c = "/"c OrElse c = "="c Then
                    run += 1
                    If run > longestRun Then longestRun = run
                Else
                    run = 0
                End If
            Next
            If longestRun > profile.EncodedBlob.LongRun Then
                res.Score += profile.Structural.LongEncodedBlob
                res.Reasons.Add($"Contains a {longestRun}-character encoded blob")
            ElseIf longestRun > profile.EncodedBlob.MediumRun Then
                res.Score += profile.Structural.MediumEncodedBlob
                res.Reasons.Add("Contains a long encoded blob")
            End If
        End Sub

        ' -- utilities ------------------------------------------------------------

        Public Shared Function Entropy(buf As Byte(), offset As Integer, count As Integer) As Double
            If count <= 0 OrElse offset < 0 OrElse offset + count > buf.Length Then Return 0
            Dim freq(255) As Integer
            For i = offset To offset + count - 1
                freq(buf(i)) += 1
            Next
            Dim e As Double = 0
            For i = 0 To 255
                If freq(i) = 0 Then Continue For
                Dim p = freq(i) / CDbl(count)
                e -= p * Math.Log(p, 2)
            Next
            Return e
        End Function

        Private Shared Function LooksTextual(buf As Byte(), count As Integer) As Boolean
            If count <= 0 Then Return False
            Dim printable = 0
            For i = 0 To count - 1
                Dim b = buf(i)
                If b = 9 OrElse b = 10 OrElse b = 13 OrElse (b >= 32 AndAlso b < 127) Then printable += 1
            Next
            Return printable / CDbl(count) > 0.9
        End Function

        Private Shared Function Latin1(buf As Byte(), count As Integer) As String
            If count <= 0 Then Return ""
            Return Encoding.Latin1.GetString(buf, 0, Math.Min(count, buf.Length))
        End Function

    End Class

End Namespace
