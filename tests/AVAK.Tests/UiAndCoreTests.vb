Imports System.Drawing
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports AVAK.Core
Imports AVAK.Theme
Imports AVAK.Ui

Namespace AVAK.Tests

    <TestClass>
    Public Class PaletteTests

        <TestMethod>
        Public Sub Mocha_matches_the_published_palette()
            Assert.AreEqual(Color.FromArgb(255, &H1E, &H1E, &H2E), Palette.Mocha.Base)
            Assert.AreEqual(Color.FromArgb(255, &HCD, &HD6, &HF4), Palette.Mocha.Text)
            Assert.AreEqual(Color.FromArgb(255, &HCB, &HA6, &HF7), Palette.Mocha.Mauve)
            Assert.AreEqual(Color.FromArgb(255, &HA6, &HE3, &HA1), Palette.Mocha.Green)
            Assert.AreEqual(Color.FromArgb(255, &H11, &H11, &H1B), Palette.Mocha.Crust)
        End Sub

        <TestMethod>
        Public Sub Latte_is_the_only_light_flavour()
            Assert.IsTrue(Palette.Mocha.IsDark)
            Assert.IsTrue(Palette.Macchiato.IsDark)
            Assert.IsTrue(Palette.Frappe.IsDark)
            Assert.IsFalse(Palette.Latte.IsDark)
        End Sub

        <TestMethod>
        Public Sub Every_accent_name_resolves()
            For Each p In Palette.All
                For Each n In Palette.AccentNames
                    Assert.AreNotEqual(Color.Empty, p.Accent(n), n & " is missing from " & p.Name)
                Next
            Next
        End Sub

        <TestMethod>
        Public Sub Unknown_accent_falls_back_to_mauve()
            Assert.AreEqual(Palette.Mocha.Mauve, Palette.Mocha.Accent("not-a-colour"))
        End Sub
    End Class

    <TestClass>
    Public Class ThemeTests

        <TestMethod>
        Public Sub Mix_does_not_overflow_on_descending_channels()
            ' regression: VB Byte-Byte arithmetic used to throw here
            Dim a = Color.FromArgb(255, 250, 250, 250)
            Dim b = Color.FromArgb(255, 0, 0, 0)
            Dim mid = ThemeManager.Mix(a, b, 0.5F)
            Assert.AreEqual(125, mid.R)
            Assert.AreEqual(125, mid.G)
        End Sub

        <TestMethod>
        Public Sub Mix_clamps_the_factor()
            Dim a = Color.FromArgb(255, 0, 0, 0)
            Dim b = Color.FromArgb(255, 255, 255, 255)
            Assert.AreEqual(a, ThemeManager.Mix(a, b, -5.0F))
            Assert.AreEqual(b, ThemeManager.Mix(a, b, 5.0F))
        End Sub

        <TestMethod>
        Public Sub Foreground_on_a_light_accent_is_dark()
            Dim fg = ThemeManager.On_(Palette.Mocha.Mauve)
            Dim lum = (0.299R * fg.R + 0.587R * fg.G + 0.114R * fg.B) / 255.0R
            Assert.IsTrue(lum < 0.3R, "text on a light accent must be dark for contrast")
        End Sub

        <TestMethod>
        Public Sub Foreground_on_a_dark_surface_is_light()
            Dim fg = ThemeManager.On_(Palette.Mocha.Base)
            Dim lum = (0.299R * fg.R + 0.587R * fg.G + 0.114R * fg.B) / 255.0R
            Assert.IsTrue(lum > 0.6R)
        End Sub

        <TestMethod>
        Public Sub Applying_a_flavour_changes_the_palette()
            ThemeManager.Apply(Flavor.Latte, "Blue", 8)
            Assert.AreEqual("Latte", ThemeManager.Colors.Name)
            Assert.AreEqual(Palette.Latte.Blue, ThemeManager.Accent)
            Assert.AreEqual(8, ThemeManager.Radius)
            ThemeManager.Apply(Flavor.Mocha, "Mauve", 12)
        End Sub
    End Class

    <TestClass>
    Public Class IconTests

        <TestMethod>
        Public Sub Every_icon_renders_without_throwing()
            Using bmp As New Bitmap(64, 64)
                Using g = Graphics.FromImage(bmp)
                    For Each name In IconNames()
                        Icons.Draw(g, name, New RectangleF(0, 0, 64, 64), Color.White, 2.0F)
                    Next
                End Using
            End Using
        End Sub

        <TestMethod>
        Public Sub Every_icon_actually_paints_pixels()
            For Each name In IconNames()
                Using bmp = Icons.ToBitmap(name, 48, Color.White)
                    Dim painted = 0
                    For y = 0 To bmp.Height - 1
                        For x = 0 To bmp.Width - 1
                            If bmp.GetPixel(x, y).A > 24 Then painted += 1
                        Next
                    Next
                    Assert.IsTrue(painted > 20, $"icon '{name}' rendered {painted} visible pixels")
                End Using
            Next
        End Sub

        <TestMethod>
        Public Sub Unknown_icon_falls_back_instead_of_throwing()
            Dim shapes = Icons.Get("definitely-not-an-icon")
            Assert.IsNotNull(shapes)
            Assert.IsTrue(shapes.Length > 0)
        End Sub

        Private Shared Function IconNames() As String()
            Return {"shield", "shield-check", "shield-alert", "shield-off", "shield-plus", "search", "scan",
                    "grid", "activity", "cpu", "memory", "disk", "globe", "wifi", "network", "lock", "unlock",
                    "user", "users", "settings", "bell", "bell-off", "clock", "history", "trash", "folder",
                    "file", "file-alert", "check", "check-circle", "x", "x-circle", "alert", "info", "power",
                    "play", "pause", "stop", "refresh", "rotate", "download", "upload", "chevron-right",
                    "chevron-left", "chevron-down", "chevron-up", "plus", "minus", "maximize", "eye", "eye-off",
                    "database", "zap", "flame", "sparkle", "key", "logout", "link", "moon", "sun", "palette",
                    "gauge", "ban", "server", "filter", "terminal", "save", "heart", "list", "cloud", "bug", "star"}
        End Function
    End Class

    <TestClass>
    Public Class SvgPathTests

        <TestMethod>
        Public Sub Simple_line_produces_a_path()
            Using gp = SvgPath.Parse("M0 0 L10 10")
                Assert.IsTrue(gp.PointCount >= 2)
            End Using
        End Sub

        <TestMethod>
        Public Sub Relative_and_absolute_agree()
            Using abs = SvgPath.Parse("M0 0 L10 0 L10 10")
                Using rel = SvgPath.Parse("M0 0 l10 0 l0 10")
                    Assert.AreEqual(abs.GetBounds().Width, rel.GetBounds().Width, 0.01F)
                    Assert.AreEqual(abs.GetBounds().Height, rel.GetBounds().Height, 0.01F)
                End Using
            End Using
        End Sub

        <TestMethod>
        Public Sub Arc_command_sweeps_a_real_curve()
            Using gp = SvgPath.Parse("M0 10 a10 10 0 0 1 20 0")
                Dim b = gp.GetBounds()
                Assert.IsTrue(b.Width > 15, "the arc should span roughly its diameter")
                Assert.IsTrue(b.Height > 5)
            End Using
        End Sub

        <TestMethod>
        Public Sub Closed_shape_has_bounds()
            Using gp = SvgPath.Parse("M0 0 H10 V10 H0 Z")
                Dim b = gp.GetBounds()
                Assert.AreEqual(10.0F, b.Width, 0.01F)
                Assert.AreEqual(10.0F, b.Height, 0.01F)
            End Using
        End Sub

        <TestMethod>
        Public Sub Negative_numbers_without_separators_parse()
            Using gp = SvgPath.Parse("M10 10 l-5-5")
                Dim b = gp.GetBounds()
                Assert.AreEqual(5.0F, b.Width, 0.01F)
            End Using
        End Sub

        <TestMethod>
        Public Sub Garbage_input_does_not_throw()
            Using gp = SvgPath.Parse("this is not a tmpFile")
                Assert.IsNotNull(gp)
            End Using
        End Sub
    End Class

    <TestClass>
    Public Class FormattingTests

        <TestMethod>
        Public Sub Bytes_scale_through_the_units()
            Assert.AreEqual("512 B", Fmt.Bytes(512))
            Assert.AreEqual("1.00 KB", Fmt.Bytes(1024))
            Assert.AreEqual("1.00 MB", Fmt.Bytes(1024L * 1024L))
            Assert.AreEqual("1.00 GB", Fmt.Bytes(1024L * 1024L * 1024L))
        End Sub

        <TestMethod>
        Public Sub Negative_size_is_rendered_as_a_dash()
            Assert.AreEqual("-", Fmt.Bytes(-1))
        End Sub

        <TestMethod>
        Public Sub Durations_pick_a_sensible_unit()
            Assert.AreEqual("500 ms", Fmt.Duration(TimeSpan.FromMilliseconds(500)))
            Assert.AreEqual("2.0 s", Fmt.Duration(TimeSpan.FromSeconds(2)))
            Assert.IsTrue(Fmt.Duration(TimeSpan.FromMinutes(3)).Contains("m"))
            Assert.IsTrue(Fmt.Duration(TimeSpan.FromHours(2)).Contains("h"))
        End Sub

        <TestMethod>
        Public Sub Ellipsis_never_exceeds_the_limit()
            Dim s = Fmt.Ellipsis("abcdefghijklmnop", 10)
            Assert.AreEqual(10, s.Length)
            Assert.IsTrue(s.EndsWith("..."))
            Assert.AreEqual("short", Fmt.Ellipsis("short", 10))
        End Sub

        <TestMethod>
        Public Sub Short_path_keeps_the_file_name()
            Dim p = "C:\a\very\deep\folder\structure\that\keeps\going\file.txt"
            Dim s = Fmt.ShortPath(p, 30)
            Assert.IsTrue(s.Contains("file.txt"))
            Assert.IsTrue(s.Length <= 34)
        End Sub

        <TestMethod>
        Public Sub Ago_handles_the_never_case()
            Assert.AreEqual("never", Fmt.Ago(DateTime.MinValue))
            Assert.AreEqual("just now", Fmt.Ago(DateTime.Now))
        End Sub
    End Class

    <TestClass>
    Public Class JsonStoreTests

        <TestMethod>
        Public Sub Settings_round_trip_through_disk()
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-cfg-" & Guid.NewGuid().ToString("N") & ".json")
            Try
                Dim s As New AppSettings() With {.AccentName = "Teal", .MaxFileSizeMb = 77, .ScanArchives = True}
                s.ExcludedPaths.Add("C:\Skip")
                Assert.IsTrue(Json.Save(tmpFile, s))

                Dim back = Json.Load(Of AppSettings)(tmpFile)
                Assert.IsNotNull(back)
                Assert.AreEqual("Teal", back.AccentName)
                Assert.AreEqual(77, back.MaxFileSizeMb)
                Assert.IsTrue(back.ScanArchives)
                Assert.AreEqual(1, back.ExcludedPaths.Count)
            Finally
                If File.Exists(tmpFile) Then File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Missing_file_returns_nothing_rather_than_throwing()
            Assert.IsNull(Json.Load(Of AppSettings)(Path.Combine(Path.GetTempPath(), "avak-does-not-exist.json")))
        End Sub

        <TestMethod>
        Public Sub Corrupt_file_is_quarantined_and_returns_nothing()
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-bad-" & Guid.NewGuid().ToString("N") & ".json")
            Try
                File.WriteAllText(tmpFile, "{ this is not json ")
                Assert.IsNull(Json.Load(Of AppSettings)(tmpFile))
                Assert.IsTrue(File.Exists(tmpFile & ".corrupt"), "the broken file should be kept for diagnosis")
            Finally
                For Each f In {tmpFile, tmpFile & ".corrupt"}
                    If File.Exists(f) Then File.Delete(f)
                Next
            End Try
        End Sub

        <TestMethod>
        Public Sub Written_json_has_no_byte_order_mark()
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-bom-" & Guid.NewGuid().ToString("N") & ".json")
            Try
                Json.Save(tmpFile, New AppSettings())
                Dim bytes = File.ReadAllBytes(tmpFile)
                Assert.IsFalse(bytes.Length > 3 AndAlso bytes(0) = &HEF AndAlso bytes(1) = &HBB AndAlso bytes(2) = &HBF,
                               "other tools should be able to read the file without a BOM")
            Finally
                If File.Exists(tmpFile) Then File.Delete(tmpFile)
            End Try
        End Sub

        <TestMethod>
        Public Sub Settings_normalisation_clamps_out_of_range_values()
            Dim s As New AppSettings() With {.CornerRadius = 999, .HeuristicSensitivity = 9, .MaxFileSizeMb = 0}
            Dim tmpFile = Path.Combine(Path.GetTempPath(), "avak-norm-" & Guid.NewGuid().ToString("N") & ".json")
            Try
                Json.Save(tmpFile, s)
                s.Save()   ' triggers Normalize
            Catch
            Finally
                If File.Exists(tmpFile) Then File.Delete(tmpFile)
            End Try
            Assert.IsTrue(s.CornerRadius <= 20)
            Assert.IsTrue(s.HeuristicSensitivity <= 3)
            Assert.IsTrue(s.MaxFileSizeMb >= 1)
        End Sub
    End Class

End Namespace
