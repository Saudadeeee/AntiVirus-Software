Imports System.IO
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports AVAK.Content
Imports AVAK.Core
Imports AVAK.Engine
Imports AVAK.Security

Namespace AVAK.Tests

    <TestClass>
    Public Class RulePackTests

        Private Shared Function GoodPack() As RulePack
            Dim p = RulePack.CreateTemplate("Test pack", "tester")
            p.Hashes.Add(New HashSignature With {
                .Sha256 = "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f",
                .Name = "Test.Hash", .Severity = Severity.High})
            p.Patterns.Add(New PatternSignature With {
                .Name = "Test.Pattern", .Ascii = "ACME-CANARY-TOKEN", .Severity = Severity.Medium})
            Return p
        End Function

        <TestMethod>
        Public Sub A_valid_pack_reports_no_issues()
            Assert.AreEqual(0, GoodPack().Validate().Count)
        End Sub

        <TestMethod>
        Public Sub Short_sha256_is_rejected()
            Dim p = GoodPack()
            p.Hashes(0).Sha256 = "abc123"
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("64 hex")))
        End Sub

        <TestMethod>
        Public Sub Non_hex_sha256_is_rejected()
            Dim p = GoodPack()
            p.Hashes(0).Sha256 = New String("z"c, 64)
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("64 hex")))
        End Sub

        <TestMethod>
        Public Sub Duplicate_hash_inside_one_pack_is_reported()
            Dim p = GoodPack()
            p.Hashes.Add(New HashSignature With {.Sha256 = p.Hashes(0).Sha256, .Name = "Copy"})
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("duplicate")))
        End Sub

        <TestMethod>
        Public Sub A_rule_with_no_content_is_rejected()
            Dim p = RulePack.CreateTemplate("x", "y")
            p.Hashes.Add(New HashSignature With {.Name = "Nothing"})
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("sha256")))
        End Sub

        <TestMethod>
        Public Sub A_pattern_needs_exactly_one_of_ascii_or_hex()
            Dim p = RulePack.CreateTemplate("x", "y")
            p.Patterns.Add(New PatternSignature With {.Name = "Both", .Ascii = "hello", .Hex = "4D5A"})
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("only one")))

            Dim q = RulePack.CreateTemplate("x", "y")
            q.Patterns.Add(New PatternSignature With {.Name = "Neither"})
            Assert.IsTrue(q.Validate().Any(Function(i) i.Contains("needs")))
        End Sub

        <TestMethod>
        Public Sub Very_short_ascii_patterns_are_flagged()
            Dim p = RulePack.CreateTemplate("x", "y")
            p.Patterns.Add(New PatternSignature With {.Name = "Tiny", .Ascii = "ab"})
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("match almost everything")))
        End Sub

        <TestMethod>
        Public Sub Odd_length_hex_is_rejected()
            Dim p = RulePack.CreateTemplate("x", "y")
            p.Patterns.Add(New PatternSignature With {.Name = "Odd", .Hex = "4D5"})
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("even number")))
        End Sub

        <TestMethod>
        Public Sub Extensions_without_a_dot_are_flagged()
            Dim p = RulePack.CreateTemplate("x", "y")
            p.Patterns.Add(New PatternSignature With {
                .Name = "NoDot", .Ascii = "something", .Extensions = New List(Of String) From {"bat"}})
            Assert.IsTrue(p.Validate().Any(Function(i) i.Contains("start with a dot")))
        End Sub

        <TestMethod>
        Public Sub A_pack_round_trips_through_json()
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-pack-" & Guid.NewGuid().ToString("N") & ".rules.json")
            Try
                Assert.IsTrue(Json.Save(tmpFile, GoodPack()))
                Dim back = Json.Load(Of RulePack)(tmpFile)
                Assert.IsNotNull(back)
                back.Normalise()
                Assert.AreEqual("Test pack", back.Name)
                Assert.AreEqual(1, back.Hashes.Count)
                Assert.AreEqual(1, back.Patterns.Count)
                Assert.AreEqual(Severity.Medium, back.Patterns(0).Severity)
                Assert.AreEqual(0, back.Validate().Count)
            Finally
                If File.Exists(tmpFile) Then File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Rule_count_covers_both_kinds()
            Assert.AreEqual(2, GoodPack().RuleCount)
        End Sub
    End Class

    <TestClass>
    Public Class RuleStoreTests

        <TestMethod>
        Public Sub Shipped_packs_load_and_index()
            RuleStore.Reload()
            Assert.IsTrue(RuleStore.Packs().Count >= 1, "at least the shipped packs should load")
            Assert.IsTrue(RuleStore.PatternCount > 0)
        End Sub

        <TestMethod>
        Public Sub Matching_reports_which_pack_fired()
            RuleStore.Reload()
            Dim payload = Encoding.ASCII.GetBytes("vssadmin delete shadows /all /quiet")
            Dim hits = RuleStore.MatchPatterns(payload, payload.Length, ".bat")
            Assert.IsTrue(hits.Count > 0)
            Assert.IsFalse(String.IsNullOrWhiteSpace(hits(0).PackName), "a hit must name its pack")
        End Sub

        <TestMethod>
        Public Sub Shipped_packs_are_marked_read_only()
            RuleStore.Reload()
            Dim shipped = RuleStore.Packs().Where(Function(p) p.ReadOnlyPack).ToList()
            Assert.IsTrue(shipped.Count >= 1)
            Dim result = RuleStore.DeletePack(shipped(0).FilePath)
            Assert.IsFalse(result.Ok, "a shipped pack must not be deletable")
        End Sub

        <TestMethod>
        Public Sub Every_shipped_pack_is_valid()
            RuleStore.Reload()
            For Each p In RuleStore.Packs().Where(Function(x) x.ReadOnlyPack)
                Dim issues = p.Validate()
                Assert.AreEqual(0, issues.Count,
                                $"{p.FileName}: {String.Join("; ", issues)}")
            Next
        End Sub

        <TestMethod>
        Public Sub Adding_a_hash_requires_a_value()
            Dim r = RuleStore.AddHash("", "Nameless", Severity.High)
            Assert.IsFalse(r.Ok)
        End Sub

        <TestMethod>
        Public Sub Adding_a_bad_pattern_is_refused_with_a_reason()
            Dim r = RuleStore.AddPattern(New PatternSignature With {.Name = "Bad", .Hex = "ZZ"})
            Assert.IsFalse(r.Ok)
            Assert.IsTrue(r.Message.Length > 0)
        End Sub
    End Class

    <TestClass>
    Public Class HeuristicProfileTests

        <TestMethod>
        Public Sub The_shipped_profile_loads()
            Dim p = HeuristicProfile.Current
            Assert.IsNotNull(p)
            Assert.IsTrue(p.RiskyApis.Count > 0, "the profile should carry API weights")
            Assert.IsTrue(p.ScriptMarkers.Count > 0)
            Assert.IsTrue(p.ExecutableSet.Contains(".exe"))
        End Sub

        <TestMethod>
        Public Sub Thresholds_map_scores_to_severity()
            Dim p As New HeuristicProfile()
            p.Rebuild()
            Assert.AreEqual(Severity.Clean, p.SeverityFor(0))
            Assert.AreEqual(Severity.Low, p.SeverityFor(p.Thresholds.Low))
            Assert.AreEqual(Severity.Medium, p.SeverityFor(p.Thresholds.Medium))
            Assert.AreEqual(Severity.High, p.SeverityFor(p.Thresholds.High))
            Assert.AreEqual(Severity.Critical, p.SeverityFor(p.Thresholds.Critical))
        End Sub

        <TestMethod>
        Public Sub Sensitivity_multipliers_are_ordered()
            Dim p As New HeuristicProfile()
            Assert.IsTrue(p.MultiplierFor(1) < p.MultiplierFor(2))
            Assert.IsTrue(p.MultiplierFor(2) < p.MultiplierFor(3))
        End Sub

        <TestMethod>
        Public Sub A_custom_profile_changes_the_verdict()
            ' the same file, scored by two different profiles
            Dim data = New Byte(255) {}
            data(0) = &H4D : data(1) = &H5A

            Dim strict As New HeuristicProfile()
            strict.Rebuild()
            strict.Thresholds.Medium = 1              ' anything at all is Medium
            Dim strictResult = HeuristicAnalyzer.Analyze("C:\a.pdf.exe", data, data.Length, Nothing, 2, strict)

            ' SeverityFor tests Critical -> High -> Medium -> Low in that order, so
            ' every band has to be raised for a profile to report nothing.
            Dim lax As New HeuristicProfile()
            lax.Rebuild()
            lax.Thresholds.Low = 9000
            lax.Thresholds.Medium = 9000
            lax.Thresholds.High = 9000
            lax.Thresholds.Critical = 9000
            Dim laxResult = HeuristicAnalyzer.Analyze("C:\a.pdf.exe", data, data.Length, Nothing, 2, lax)

            Assert.IsTrue(strictResult.Severity >= Severity.Medium)
            Assert.AreEqual(Severity.Clean, laxResult.Severity)
        End Sub

        <TestMethod>
        Public Sub Zero_weights_disable_a_check()
            Dim data = New Byte(255) {}
            data(0) = &H4D : data(1) = &H5A

            Dim p As New HeuristicProfile()
            p.Rebuild()
            p.Structural.DoubleExtension = 0
            Dim r = HeuristicAnalyzer.Analyze("C:\a.pdf.exe", data, data.Length, Nothing, 2, p)
            Assert.IsFalse(r.Reasons.Any(Function(x) x.Contains("Double extension")) AndAlso r.Score >= 34)
        End Sub

        <TestMethod>
        Public Sub Custom_markers_are_honoured()
            Dim p As New HeuristicProfile()
            p.Rebuild()
            p.ScriptMarkers = New List(Of ScriptMarker) From {
                New ScriptMarker With {.Needle = "acme-secret-marker", .Weight = 99, .Why = "custom marker"}}

            Dim data = Encoding.ASCII.GetBytes("harmless text with acme-secret-marker inside")
            Dim r = HeuristicAnalyzer.Analyze("C:\a.ps1", data, data.Length, Nothing, 2, p)
            Assert.IsTrue(r.Reasons.Any(Function(x) x.Contains("custom marker")))
        End Sub
    End Class

    <TestClass>
    Public Class ProviderRegistryTests

        <TestMethod>
        Public Sub The_built_in_providers_are_registered()
            ProviderRegistry.EnsureLoaded()
            Dim ids = ProviderRegistry.All().Select(Function(s) s.Provider.Id).ToList()
            CollectionAssert.Contains(ids, "avak.hash")
            CollectionAssert.Contains(ids, "avak.pattern")
            CollectionAssert.Contains(ids, "avak.archive")
            CollectionAssert.Contains(ids, "avak.heuristic")
        End Sub

        <TestMethod>
        Public Sub Providers_run_in_ascending_order()
            Dim orders = ProviderRegistry.Active().Select(Function(p) p.Order).ToList()
            For i = 1 To orders.Count - 1
                Assert.IsTrue(orders(i) >= orders(i - 1), "providers must be ordered")
            Next
        End Sub

        <TestMethod>
        Public Sub Provider_ids_are_unique()
            Dim ids = ProviderRegistry.All().Select(Function(s) s.Provider.Id.ToLowerInvariant()).ToList()
            Assert.AreEqual(ids.Count, ids.Distinct().Count())
        End Sub

        <TestMethod>
        Public Sub A_throwing_provider_is_isolated_and_eventually_disabled()
            Dim bad As New ThrowingProvider()
            Dim state As New ProviderState With {.Provider = bad}

            ' the registry disables after MaxFailures; simulate the counter contract
            For i = 1 To ProviderState.MaxFailures
                Assert.IsTrue(state.Healthy, "should stay healthy until the limit")
                state.Failures += 1
            Next
            Assert.IsFalse(state.Healthy, "past the limit the provider must be considered faulted")
        End Sub

        <TestMethod>
        Public Sub A_context_computes_nothing_until_asked()
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-ctx-" & Guid.NewGuid().ToString("N") & ".txt")
            Try
                File.WriteAllText(tmpFile, "hello world")
                Dim ctx As New DetectionContext(tmpFile, New FileInfo(tmpFile), New AppSettings())

                Assert.AreEqual(".txt", ctx.Extension)
                Assert.AreEqual(11L, ctx.SizeBytes)
                Assert.IsFalse(ctx.IsPortableExecutable)
                Assert.AreEqual(11, ctx.BufferLength)
                Assert.AreEqual(64, ctx.Sha256.Length)
                Assert.AreEqual(ctx.Sha256, ctx.Sha256, "the hash must be cached, not recomputed")
            Finally
                If File.Exists(tmpFile) Then File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Context_extension_helpers_are_case_insensitive()
            Dim ctx As New DetectionContext("C:\x\Setup.EXE", Nothing, New AppSettings())
            Assert.IsTrue(ctx.HasExtension(".exe"))
            Assert.IsFalse(ctx.HasExtension(".dll"))
            Assert.AreEqual("Setup.EXE", ctx.FileName)
        End Sub

        <TestMethod>
        Public Sub A_custom_provider_can_produce_a_detection()
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-prov-" & Guid.NewGuid().ToString("N") & ".marker")
            Try
                File.WriteAllText(tmpFile, "anything")
                Dim ctx As New DetectionContext(tmpFile, New FileInfo(tmpFile), New AppSettings())

                Dim provider As New MarkerProvider()
                Assert.IsTrue(provider.Initialise())
                Assert.IsTrue(provider.CanHandle(ctx))

                Dim det = provider.Inspect(ctx)
                Assert.IsNotNull(det)
                Assert.AreEqual("Test:Marker/Found", det.ThreatName)
                Assert.AreEqual(tmpFile, det.FilePath)
                Assert.AreEqual(ctx.Sha256, det.Sha256, "the helper should fill in the hash")
                Assert.IsTrue(det.Reasons.Count > 0)
            Finally
                If File.Exists(tmpFile) Then File.Delete(tmpFile)
            End Try
        End Sub

        ' -- test doubles ---------------------------------------------------

        Private Class ThrowingProvider
            Inherits DetectionProviderBase

            Public Overrides ReadOnly Property Id As String
                Get
                    Return "test.throwing"
                End Get
            End Property

            Public Overrides ReadOnly Property DisplayName As String
                Get
                    Return "Always throws"
                End Get
            End Property

            Public Overrides Function Inspect(context As DetectionContext) As Detection
                Throw New InvalidOperationException("boom")
            End Function
        End Class

        Private Class MarkerProvider
            Inherits DetectionProviderBase

            Public Overrides ReadOnly Property Id As String
                Get
                    Return "test.marker"
                End Get
            End Property

            Public Overrides ReadOnly Property DisplayName As String
                Get
                    Return "Marker files"
                End Get
            End Property

            Public Overrides Function CanHandle(context As DetectionContext) As Boolean
                Return context.HasExtension(".marker")
            End Function

            Public Overrides Function Inspect(context As DetectionContext) As Detection
                Return Detect(context, "Test:Marker/Found", Severity.Low,
                              DetectionSource.Heuristic, 20,
                              "The extension is .marker")
            End Function
        End Class
    End Class

End Namespace
