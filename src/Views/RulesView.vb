Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports AVAK.Content
Imports AVAK.Core
Imports AVAK.Engine
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>
    ''' The extensibility surface: detection providers and rule packs, both
    ''' switchable, plus the tools to add rules without leaving the app.
    ''' </summary>
    Public Class RulesView
        Inherits ViewBase

        Private _tabs As CatSegment

        Private _providerCard As CatCard
        Private _providerTable As CatTable
        Private _providerToggle As CatButton
        Private _providerReload As CatButton
        Private _openPlugins As CatButton
        Private _providerHint As CatLabel

        Private _packCard As CatCard
        Private _packTable As CatTable
        Private _packToggle As CatButton
        Private _packNew As CatButton
        Private _packImport As CatButton
        Private _packOpen As CatButton
        Private _packDelete As CatButton
        Private _packHint As CatLabel

        Private _addCard As CatCard
        Private _addHashFile As CatButton
        Private _addHashText As CatButton
        Private _addPattern As CatButton
        Private _validate As CatButton
        Private _testFolder As CatButton
        Private _editHeuristics As CatButton
        Private _addHint As CatLabel

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Rules"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Detection providers, rule packs and everything you can extend"
            End Get
        End Property

        Protected Overrides Sub Build()
            _tabs = New CatSegment()
            _tabs.SetItems("Providers", "Rule packs")
            AddHandler _tabs.SelectionChanged, Sub()
                                                   SwitchTab()
                                                   Relayout()
                                               End Sub
            Controls.Add(_tabs)

            ' -- providers
            _providerCard = Card("Detection providers", "cpu")
            _providerCard.ReserveSubtitle = True
            _providerTable = New CatTable With {
                .RowHeight = 44, .EmptyText = "No providers registered", .EmptyIcon = "cpu"}
            _providerTable.SetColumns(New CatColumn("Provider", 0),
                                      New CatColumn("Id", 150, StringAlignment.Near, True),
                                      New CatColumn("Order", 66, StringAlignment.Far, True),
                                      New CatColumn("Source", 130, StringAlignment.Near, True),
                                      New CatColumn("Hits", 70, StringAlignment.Far),
                                      New CatColumn("Time", 84, StringAlignment.Far, True),
                                      New CatColumn("State", 96))
            AddHandler _providerTable.RowActivated, Sub() ToggleProvider()
            _providerCard.Controls.Add(_providerTable)

            _providerToggle = Btn("Enable / disable", ButtonKind.Secondary, "power", Sub() ToggleProvider(), _providerCard)
            _providerReload = Btn("Reload plugins", ButtonKind.Ghost, "refresh",
                                  Sub()
                                      ProviderRegistry.Reload()
                                      RefreshProviders()
                                      Say("Providers reloaded.")
                                  End Sub, _providerCard)
            _openPlugins = Btn("Open plugins folder", ButtonKind.Ghost, "folder",
                               Sub() OpenFolder(ProviderRegistry.PluginFolder), _providerCard)
            _providerHint = Lbl("", "small", ThemeManager.FgMuted, _providerCard)
            _providerHint.Wrap = True
            _providerHint.VAlign = StringAlignment.Near
            _providerHint.SetText(
                "A provider is one way of deciding whether a file is malicious. Implement " &
                "AVAK.Engine.IDetectionProvider in a .NET 8 class library, drop the DLL in the plugins " &
                "folder and it appears here. Order decides who runs first; a terminal provider stops the chain." &
                Environment.NewLine &
                "Plugins run with AVAK's own rights - only load DLLs you built or audited.")

            ' -- packs
            _packCard = Card("Rule packs", "database")
            _packCard.ReserveSubtitle = True
            _packTable = New CatTable With {
                .RowHeight = 44, .EmptyText = "No rule packs found", .EmptyIcon = "database"}
            _packTable.SetColumns(New CatColumn("Pack", 0),
                                  New CatColumn("Version", 96, StringAlignment.Near, True),
                                  New CatColumn("Hashes", 80, StringAlignment.Far),
                                  New CatColumn("Patterns", 84, StringAlignment.Far),
                                  New CatColumn("Origin", 110, StringAlignment.Near, True),
                                  New CatColumn("State", 96))
            AddHandler _packTable.RowActivated, Sub() TogglePack()
            _packCard.Controls.Add(_packTable)

            _packToggle = Btn("Enable / disable", ButtonKind.Secondary, "power", Sub() TogglePack(), _packCard)
            _packNew = Btn("New pack", ButtonKind.Ghost, "plus", Sub() NewPack(), _packCard)
            _packImport = Btn("Import", ButtonKind.Ghost, "upload", Sub() ImportPack(), _packCard)
            _packOpen = Btn("Open folder", ButtonKind.Ghost, "folder",
                            Sub() OpenFolder(RuleStore.UserFolder), _packCard)
            _packDelete = Btn("Delete", ButtonKind.Danger, "trash", Sub() DeletePack(), _packCard)
            _packHint = Lbl("", "small", ThemeManager.FgMuted, _packCard)
            _packHint.Wrap = True
            _packHint.VAlign = StringAlignment.Near

            ' -- authoring
            _addCard = Card("Add and test rules", "sparkle")
            _addHashFile = Btn("Add hash from file", ButtonKind.Primary, "file", Sub() AddHashFromFile(), _addCard)
            _addHashText = Btn("Add hash by value", ButtonKind.Secondary, "key", Sub() AddHashByValue(), _addCard)
            _addPattern = Btn("Add pattern", ButtonKind.Secondary, "search", Sub() AddPattern(), _addCard)
            _validate = Btn("Validate all", ButtonKind.Ghost, "check-circle", Sub() ValidateAll(), _addCard)
            _testFolder = Btn("Test on a folder", ButtonKind.Ghost, "scan", Sub() TestFolder(), _addCard)
            _editHeuristics = Btn("Edit heuristics", ButtonKind.Ghost, "settings", Sub() EditHeuristics(), _addCard)
            _addHint = Lbl("", "small", ThemeManager.FgMuted, _addCard)
            _addHint.Wrap = True
            _addHint.VAlign = StringAlignment.Near

            SwitchTab()
        End Sub

        Public Overrides Sub OnActivated()
            RefreshProviders()
            RefreshPacks()
            RefreshHints()
        End Sub

        Private Sub SwitchTab()
            Dim providers = _tabs.SelectedIndex = 0
            _providerCard.Visible = providers
            _packCard.Visible = Not providers
        End Sub

        ' -- providers ---------------------------------------------------------

        Private Sub RefreshProviders()
            Dim states = ProviderRegistry.All()
            _providerTable.SetRows(states.OrderBy(Function(s) s.Provider.Order).Select(
                Function(s) New CatRow(s, s.Provider.DisplayName, s.Provider.Id,
                                       s.Provider.Order.ToString(), s.Source,
                                       Fmt.Num(s.Detections),
                                       If(s.ElapsedMs > 0, s.ElapsedMs & " ms", "-"),
                                       ProviderState_(s)) With {
                    .IconName = If(Not s.Healthy, "alert", If(s.Enabled, "check-circle", "ban")),
                    .Tone = If(Not s.Healthy, ThemeManager.Danger,
                               If(s.Enabled, ThemeManager.Ok, ThemeManager.FgDim))}).ToList())

            Dim active = states.Where(Function(s) s.Enabled AndAlso s.Healthy).Count()
            _providerCard.Subtitle = $"{active} active of {states.Count} - plugins load from {ProviderRegistry.PluginFolder}"
            _providerCard.Invalidate()
        End Sub

        Private Shared Function ProviderState_(s As ProviderState) As String
            If Not s.Healthy Then Return "Faulted"
            Return If(s.Enabled, "Enabled", "Disabled")
        End Function

        Private Sub ToggleProvider()
            Dim r = _providerTable.SelectedRows.FirstOrDefault()
            If r Is Nothing Then
                Say("Select a provider first.", True)
                Return
            End If
            Dim s = CType(r.Tag, ProviderState)
            ProviderRegistry.SetEnabled(s.Provider.Id, Not s.Enabled)
            RefreshProviders()
            Say($"{s.Provider.DisplayName} is now {If(s.Enabled, "disabled", "enabled")}.")
        End Sub

        ' -- packs -------------------------------------------------------------

        Private Sub RefreshPacks()
            Dim packs = RuleStore.Packs()
            _packTable.SetRows(packs.Select(
                Function(p) New CatRow(p, p.Name, p.Version,
                                       p.Hashes.Count.ToString(), p.Patterns.Count.ToString(),
                                       If(p.ReadOnlyPack, "shipped", "yours"),
                                       If(p.Issues.Count > 0, "Has issues", If(p.Enabled, "Enabled", "Disabled"))) With {
                    .IconName = If(p.Issues.Count > 0, "alert", If(p.Enabled, "check-circle", "ban")),
                    .Tone = If(p.Issues.Count > 0, ThemeManager.Warn,
                               If(p.Enabled, ThemeManager.Ok, ThemeManager.FgDim))}).ToList())

            _packCard.Subtitle = $"{RuleStore.HashCount} hash signatures and {RuleStore.PatternCount} patterns active"
            _packCard.Invalidate()
            RefreshHints()
        End Sub

        Private Sub RefreshHints()
            _packHint.SetText(
                "Shipped packs: " & RuleStore.BuiltInFolder & Environment.NewLine &
                "Your packs:    " & RuleStore.UserFolder & Environment.NewLine &
                "A pack is plain JSON. Add one by hand, generate it from a feed, or use the buttons below. " &
                "Double-click a row to switch a pack on or off.")

            _addHint.SetText(
                "Everything here is also available from the command line, so a script or CI job can maintain rules:" &
                Environment.NewLine &
                "  AVAK.exe --rules add-hash --file bad.exe --name Trojan.Example --severity High" & Environment.NewLine &
                "  AVAK.exe --rules add-pattern --ascii ""text"" --name My.Rule --ext .bat,.cmd" & Environment.NewLine &
                "  AVAK.exe --rules validate      AVAK.exe --rules test C:\clean-corpus" & Environment.NewLine &
                "Heuristic weights live in " & HeuristicProfile.Current.SourcePath & " and are editable JSON.")
        End Sub

        Private Function SelectedPack() As RulePack
            Dim r = _packTable.SelectedRows.FirstOrDefault()
            Return If(r Is Nothing, Nothing, CType(r.Tag, RulePack))
        End Function

        Private Sub TogglePack()
            Dim p = SelectedPack()
            If p Is Nothing Then
                Say("Select a pack first.", True)
                Return
            End If
            RuleStore.SetPackEnabled(p.FileName, Not p.Enabled)
            RefreshPacks()
            Say($"{p.Name} is now {If(p.Enabled, "disabled", "enabled")}.")
        End Sub

        Private Sub NewPack()
            Dim name = CatDialog.Prompt(FindForm(), "New rule pack",
                                        "A name for the pack. The file is created in your rules folder.",
                                        "", "My rules")
            If String.IsNullOrWhiteSpace(name) Then Return
            Dim created = RuleStore.CreatePack(name, Environment.UserName)
            RefreshPacks()
            Say("Created " & Path.GetFileName(created) & ".")
        End Sub

        Private Sub ImportPack()
            Using dlg As New OpenFileDialog() With {.Title = "Import a rule pack", .Filter = "Rule pack (*.json)|*.json"}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                Dim r = RuleStore.ImportPack(dlg.FileName)
                RefreshPacks()
                Say(r.Message, Not r.Ok)
            End Using
        End Sub

        Private Sub DeletePack()
            Dim p = SelectedPack()
            If p Is Nothing Then
                Say("Select a pack first.", True)
                Return
            End If
            If p.ReadOnlyPack Then
                Say("Shipped packs cannot be deleted - disable it instead.", True)
                Return
            End If
            If Not AskDestructive("Delete '" & p.Name & "'?",
                                  p.FilePath & Environment.NewLine & Environment.NewLine &
                                  $"{p.RuleCount} rule(s) will be lost.", "Delete") Then Return
            Dim r = RuleStore.DeletePack(p.FilePath)
            RefreshPacks()
            Say(r.Message, Not r.Ok)
        End Sub

        ' -- authoring ---------------------------------------------------------

        Private Function ChoosePack() As String
            Dim writable = RuleStore.Packs().Where(Function(p) Not p.ReadOnlyPack).ToList()
            If writable.Count = 0 Then Return RuleStore.DefaultUserPack()

            Dim selected = SelectedPack()
            If selected IsNot Nothing AndAlso Not selected.ReadOnlyPack Then Return selected.FilePath
            Return writable(0).FilePath
        End Function

        Private Sub AddHashFromFile()
            Using dlg As New OpenFileDialog() With {.Title = "Pick the file to fingerprint"}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return

                Dim sha = SignatureDatabase.Sha256File(dlg.FileName)
                Dim md5 = SignatureDatabase.Md5File(dlg.FileName)
                If sha.Length = 0 Then
                    Say("The file could not be read.", True)
                    Return
                End If

                Dim name = CatDialog.Prompt(FindForm(), "Name this detection",
                    Path.GetFileName(dlg.FileName) & Environment.NewLine &
                    "SHA-256: " & sha & Environment.NewLine & Environment.NewLine &
                    "What should AVAK call it when it sees this file again?",
                    "Custom." & Path.GetFileNameWithoutExtension(dlg.FileName), "Trojan.Example")
                If String.IsNullOrWhiteSpace(name) Then Return

                Dim sev = AskSeverity()
                If Not sev.HasValue Then Return

                Dim r = RuleStore.AddHash(sha, name, sev.Value, md5, ChoosePack())
                RefreshPacks()
                Say(r.Message, Not r.Ok)
            End Using
        End Sub

        Private Sub AddHashByValue()
            Dim sha = CatDialog.Prompt(FindForm(), "Add a hash",
                                       "Paste a SHA-256 (64 hex characters).", "", "sha256")
            If String.IsNullOrWhiteSpace(sha) Then Return

            Dim name = CatDialog.Prompt(FindForm(), "Name this detection",
                                        "What should AVAK call it?", "", "Trojan.Example")
            If String.IsNullOrWhiteSpace(name) Then Return

            Dim sev = AskSeverity()
            If Not sev.HasValue Then Return

            Dim r = RuleStore.AddHash(sha.Trim(), name, sev.Value, "", ChoosePack())
            RefreshPacks()
            Say(r.Message, Not r.Ok)
        End Sub

        Private Sub AddPattern()
            Dim text = CatDialog.Prompt(FindForm(), "Add a pattern",
                "The text AVAK should look for inside files." & Environment.NewLine &
                "Keep it specific - a short string will match everything and create false positives.",
                "", "vssadmin delete shadows")
            If String.IsNullOrWhiteSpace(text) Then Return

            Dim name = CatDialog.Prompt(FindForm(), "Name this detection",
                                        "What should AVAK call it?", "", "Suspicious.Custom")
            If String.IsNullOrWhiteSpace(name) Then Return

            Dim exts = CatDialog.Prompt(FindForm(), "Limit by extension (optional)",
                "Comma-separated, e.g. .bat,.cmd,.ps1" & Environment.NewLine &
                "Leave empty to check every file type.", "", ".bat,.cmd,.ps1")
            If exts Is Nothing Then Return

            Dim sev = AskSeverity()
            If Not sev.HasValue Then Return

            Dim p As New PatternSignature With {
                .Name = name, .Ascii = text, .Severity = sev.Value, .MaxOffset = 0}
            If Not String.IsNullOrWhiteSpace(exts) Then
                p.Extensions = exts.Split(","c).Select(Function(e) e.Trim()).
                    Where(Function(e) e.Length > 0).
                    Select(Function(e) If(e.StartsWith("."), e, "." & e)).ToList()
            End If

            Dim r = RuleStore.AddPattern(p, ChoosePack())
            RefreshPacks()
            Say(r.Message, Not r.Ok)
        End Sub

        Private Function AskSeverity() As Severity?
            Dim answer = CatDialog.Prompt(FindForm(), "Severity",
                "Low, Medium, High or Critical." & Environment.NewLine &
                "Medium and above are reported; Low is recorded but not surfaced.",
                "High", "High")
            If answer Is Nothing Then Return Nothing
            Dim sev As Severity
            If [Enum].TryParse(answer.Trim(), True, sev) Then Return sev
            Return Severity.High
        End Function

        Private Sub ValidateAll()
            RuleStore.Reload()
            Dim packs = RuleStore.Packs()
            Dim sb As New System.Text.StringBuilder()
            Dim problems = 0

            For Each p In packs
                Dim issues = p.Validate()
                If issues.Count = 0 Then
                    sb.AppendLine($"OK    {p.FileName}  ({p.RuleCount} rules)")
                Else
                    problems += issues.Count
                    sb.AppendLine($"FAIL  {p.FileName}")
                    For Each i In issues.Take(8)
                        sb.AppendLine("        " & i)
                    Next
                End If
            Next

            RefreshPacks()
            CatDialog.Alert(FindForm(), If(problems = 0, "All packs are valid", $"{problems} problem(s)"),
                            sb.ToString(), If(problems = 0, DialogTone.Success, DialogTone.Warning))
        End Sub

        Private Async Sub TestFolder()
            Using dlg As New FolderBrowserDialog() With {
                .Description = "Pick a folder to test the rules against. A known-clean folder turns every hit into a false positive."}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return

                _testFolder.Busy = True
                Try
                    Dim folder = dlg.SelectedPath
                    Dim cfg = AppSettings.Current
                    Dim report = Await Task.Run(
                        Function()
                            Dim sb As New System.Text.StringBuilder()
                            Dim files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                            Dim hits = 0
                            For Each f In files
                                Try
                                    Dim fi As New FileInfo(f)
                                    If Not fi.Exists OrElse fi.Length = 0 Then Continue For
                                    Dim det = ScanEngine.InspectFile(f, fi, cfg)
                                    If det Is Nothing Then Continue For
                                    hits += 1
                                    If hits <= 40 Then
                                        sb.AppendLine($"{det.Severity,-8} {det.ThreatName}")
                                        sb.AppendLine("         " & Fmt.ShortPath(f, 70))
                                    End If
                                Catch
                                End Try
                            Next
                            sb.Insert(0, $"{hits} detection(s) in {files.Length} file(s)" &
                                         Environment.NewLine & Environment.NewLine)
                            If hits > 40 Then sb.AppendLine($"... and {hits - 40} more")
                            Return sb.ToString()
                        End Function)

                    CatDialog.Alert(FindForm(), "Rule test - " & Path.GetFileName(folder),
                                    report, DialogTone.Info)
                Finally
                    _testFolder.Busy = False
                End Try
            End Using
        End Sub

        Private Sub EditHeuristics()
            Dim target = HeuristicProfile.Current.SourcePath
            If Not File.Exists(target) OrElse target.StartsWith(AppPaths.AppDir, StringComparison.OrdinalIgnoreCase) Then
                If Not Ask("Create your own heuristics file?",
                           "AVAK will copy the shipped weights to:" & Environment.NewLine &
                           HeuristicProfile.UserPath & Environment.NewLine & Environment.NewLine &
                           "Your copy wins over the shipped one and survives updates.",
                           "Create") Then Return
                target = HeuristicProfile.CreateUserCopy()
            End If

            Try
                Process.Start(New ProcessStartInfo("notepad.exe", target) With {.UseShellExecute = True})
                Say("Save the file, then press Reload plugins to apply.")
            Catch ex As Exception
                Say("Could not open the editor: " & ex.Message, True)
            End Try
        End Sub

        Private Sub OpenFolder(folder As String)
            Try
                Process.Start(New ProcessStartInfo(folder) With {.UseShellExecute = True})
            Catch ex As Exception
                Say("Could not open " & folder & ": " & ex.Message, True)
            End Try
        End Sub

        ' -- layout ------------------------------------------------------------

        Public Overrides Sub Relayout()
            If _tabs Is Nothing Then Return
            Dim w = Math.Max(640, Width - Pad * 2)

            _tabs.SetBounds(Pad, Pad, 300, 38)

            Dim y = Pad + 50
            Dim listH = Math.Max(240, CInt((Height - y - Pad) * 0.58))

            Dim listRect = New Rectangle(Pad, y, w, listH)
            _providerCard.Bounds = listRect
            _packCard.Bounds = listRect

            Dim t1 = _providerCard.ContentTop
            _providerTable.SetBounds(8, t1 - 6, w - 16, listH - t1 - 96)
            _providerHint.SetBounds(16, listH - 98, w - 32, 48)
            _providerToggle.SetBounds(12, listH - 48, 160, 38)
            _providerReload.SetBounds(180, listH - 48, 150, 38)
            _openPlugins.SetBounds(338, listH - 48, 186, 38)

            Dim t2 = _packCard.ContentTop
            _packTable.SetBounds(8, t2 - 6, w - 16, listH - t2 - 106)
            _packHint.SetBounds(16, listH - 108, w - 32, 58)
            _packToggle.SetBounds(12, listH - 48, 160, 38)
            _packNew.SetBounds(180, listH - 48, 128, 38)
            _packImport.SetBounds(316, listH - 48, 112, 38)
            _packOpen.SetBounds(436, listH - 48, 142, 38)
            _packDelete.SetBounds(w - 116, listH - 48, 104, 38)

            Dim y2 = y + listH + 16
            Dim h2 = Math.Max(170, Height - y2 - Pad)
            _addCard.SetBounds(Pad, y2, w, h2)
            Dim t3 = _addCard.ContentTop
            _addHashFile.SetBounds(14, t3 - 6, 176, 40)
            _addHashText.SetBounds(198, t3 - 6, 168, 40)
            _addPattern.SetBounds(374, t3 - 6, 140, 40)
            _validate.SetBounds(522, t3 - 6, 130, 40)
            _testFolder.SetBounds(660, t3 - 6, 156, 40)
            _editHeuristics.SetBounds(824, t3 - 6, 152, 40)
            _addHint.SetBounds(16, t3 + 44, w - 32, h2 - t3 - 50)
        End Sub

    End Class

End Namespace
