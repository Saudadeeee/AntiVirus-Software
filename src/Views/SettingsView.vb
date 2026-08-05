Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.SystemInfo
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Appearance, behaviour, exclusions and signature database management.</summary>
    Public Class SettingsView
        Inherits ViewBase

        Private _scroll As CatScrollPanel

        Private _lookCard As CatCard
        Private _flavor As CatCombo
        Private _accent As CatCombo
        Private _radius As CatSegment
        Private _anim As CatToggle
        Private _swatches As Panel

        Private _behaviourCard As CatCard
        Private _autoStart As CatToggle
        Private _startMin As CatToggle
        Private _minTray As CatToggle
        Private _closeTray As CatToggle
        Private _toasts As CatToggle
        Private _sounds As CatToggle
        Private _confirmDelete As CatToggle

        Private _engineCard As CatCard
        Private _threads As CatCombo
        Private _excl As CatTable
        Private _addExcl As CatButton
        Private _addExclFile As CatButton
        Private _delExcl As CatButton

        Private _sigCard As CatCard
        Private _sigInfo As CatLabel
        Private _sigReload As CatButton
        Private _sigImport As CatButton
        Private _sigUrl As CatInput
        Private _sigUpdate As CatButton

        Private _integCard As CatCard
        Private _shellMenu As CatToggle
        Private _updateUrl As CatInput
        Private _checkAtStart As CatToggle
        Private _checkNow As CatButton
        Private _updateStatus As CatLabel

        Private _aboutCard As CatCard
        Private _aboutText As CatLabel
        Private _openData As CatButton
        Private _resetAll As CatButton

        Public Event ThemeApplied()

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Settings"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Appearance, behaviour and the detection database"
            End Get
        End Property

        Protected Overrides Sub Build()
            Dim cfg = AppSettings.Current

            _scroll = New CatScrollPanel()
            Controls.Add(_scroll)
            Dim host = _scroll.Content

            ' -- appearance
            _lookCard = New CatCard With {.Title = "Appearance", .IconName = "palette"}
            host.Controls.Add(_lookCard)

            _flavor = New CatCombo With {.IconName = "moon"}
            _flavor.SetItems({"Mocha (dark)", "Macchiato (dark)", "Frappe (dark)", "Latte (light)"})
            _flavor.SelectedIndex = CInt(cfg.Flavor)
            AddHandler _flavor.SelectionChanged, AddressOf ApplyLook
            _lookCard.Controls.Add(_flavor)

            _accent = New CatCombo With {.IconName = "sparkle"}
            _accent.SetItems(Palette.AccentNames)
            _accent.SelectedItem = cfg.AccentName
            If _accent.SelectedIndex < 0 Then _accent.SelectedIndex = 3
            AddHandler _accent.SelectionChanged, AddressOf ApplyLook
            _lookCard.Controls.Add(_accent)

            _radius = New CatSegment()
            _radius.SetItems("Sharp", "Soft", "Round")
            _radius.SelectedIndex = If(cfg.CornerRadius <= 4, 0, If(cfg.CornerRadius <= 12, 1, 2))
            AddHandler _radius.SelectionChanged, AddressOf ApplyLook
            _lookCard.Controls.Add(_radius)

            _anim = New CatToggle("Animations", "Hover, toggle and progress easing", cfg.Animations)
            AddHandler _anim.CheckedChanged, Sub()
                                                 cfg.Animations = _anim.Checked
                                                 cfg.Save()
                                             End Sub
            _lookCard.Controls.Add(_anim)

            _swatches = New Panel With {.BackColor = Color.Transparent}
            AddHandler _swatches.Paint, AddressOf PaintSwatches
            _lookCard.Controls.Add(_swatches)

            ' -- behaviour
            _behaviourCard = New CatCard With {.Title = "Behaviour", .IconName = "settings"}
            host.Controls.Add(_behaviourCard)

            _autoStart = New CatToggle("Start AVAK with Windows", "Adds an entry under HKCU\Run",
                                       StartupManager.IsSelfAutoStart())
            AddHandler _autoStart.CheckedChanged, Sub()
                                                      StartupManager.SetSelfAutoStart(_autoStart.Checked)
                                                      cfg.StartWithWindows = _autoStart.Checked
                                                      cfg.Save()
                                                  End Sub
            _behaviourCard.Controls.Add(_autoStart)

            _startMin = New CatToggle("Start minimised to the tray", "", cfg.StartMinimized)
            AddHandler _startMin.CheckedChanged, Sub()
                                                     cfg.StartMinimized = _startMin.Checked
                                                     cfg.Save()
                                                 End Sub
            _behaviourCard.Controls.Add(_startMin)

            _minTray = New CatToggle("Minimise to the tray", "Instead of the taskbar", cfg.MinimizeToTray)
            AddHandler _minTray.CheckedChanged, Sub()
                                                    cfg.MinimizeToTray = _minTray.Checked
                                                    cfg.Save()
                                                End Sub
            _behaviourCard.Controls.Add(_minTray)

            _closeTray = New CatToggle("Closing the window keeps AVAK running", "Real-time protection stays active", cfg.CloseToTray)
            AddHandler _closeTray.CheckedChanged, Sub()
                                                      cfg.CloseToTray = _closeTray.Checked
                                                      cfg.Save()
                                                  End Sub
            _behaviourCard.Controls.Add(_closeTray)

            _toasts = New CatToggle("Show notifications", "Toasts for scans, detections and cleanups", cfg.ShowToasts)
            AddHandler _toasts.CheckedChanged, Sub()
                                                   cfg.ShowToasts = _toasts.Checked
                                                   cfg.Save()
                                               End Sub
            _behaviourCard.Controls.Add(_toasts)

            _sounds = New CatToggle("Play a sound on detection", "Uses the Windows sound scheme", cfg.PlaySounds)
            AddHandler _sounds.CheckedChanged, Sub()
                                                   cfg.PlaySounds = _sounds.Checked
                                                   cfg.Save()
                                                   If _sounds.Checked Then Core.Sfx.Success()
                                               End Sub
            _behaviourCard.Controls.Add(_sounds)

            _confirmDelete = New CatToggle("Confirm before deleting", "Ask again before any irreversible delete",
                                           cfg.ConfirmBeforeDelete)
            AddHandler _confirmDelete.CheckedChanged, Sub()
                                                          cfg.ConfirmBeforeDelete = _confirmDelete.Checked
                                                          cfg.Save()
                                                      End Sub
            _behaviourCard.Controls.Add(_confirmDelete)

            ' -- engine / exclusions
            _engineCard = New CatCard With {.Title = "Scan engine", .IconName = "cpu"}
            host.Controls.Add(_engineCard)

            _threads = New CatCombo With {.IconName = "cpu"}
            Dim options As New List(Of String) From {"Automatic"}
            For i = 1 To Math.Max(1, Environment.ProcessorCount)
                options.Add(i & " thread" & If(i > 1, "s", ""))
            Next
            _threads.SetItems(options)
            _threads.SelectedIndex = cfg.ScanThreads
            AddHandler _threads.SelectionChanged, Sub()
                                                      cfg.ScanThreads = _threads.SelectedIndex
                                                      cfg.Save()
                                                  End Sub
            _engineCard.Controls.Add(_threads)

            _excl = New CatTable With {
                .RowHeight = 32, .HeaderHeight = 0, .Sortable = False,
                .EmptyText = "Nothing is excluded", .EmptyIcon = "check-circle"}
            _excl.SetColumns(New CatColumn("Excluded path", 0))
            _engineCard.Controls.Add(_excl)

            _addExcl = Btn("Exclude folder", ButtonKind.Secondary, "folder", AddressOf AddExclusionFolder, _engineCard)
            _addExclFile = Btn("Exclude file", ButtonKind.Secondary, "file", AddressOf AddExclusionFile, _engineCard)
            _delExcl = Btn("Remove", ButtonKind.Ghost, "minus", AddressOf RemoveExclusion, _engineCard)

            ' -- signatures
            _sigCard = New CatCard With {.Title = "Signature database", .IconName = "database"}
            host.Controls.Add(_sigCard)
            _sigInfo = Lbl("", "small", ThemeManager.FgMuted, _sigCard)
            _sigInfo.Wrap = True
            _sigInfo.VAlign = StringAlignment.Near
            _sigReload = Btn("Reload", ButtonKind.Secondary, "refresh",
                             Sub()
                                 SignatureDatabase.Reload()
                                 RefreshSignatures()
                                 Say("Signature database reloaded.")
                             End Sub, _sigCard)
            _sigImport = Btn("Import file...", ButtonKind.Secondary, "upload", AddressOf ImportSignatures, _sigCard)
            _sigUrl = New CatInput With {.Placeholder = "https://example.com/signatures.json", .IconName = "link"}
            _sigCard.Controls.Add(_sigUrl)
            _sigUpdate = Btn("Update from URL", ButtonKind.Primary, "download", AddressOf UpdateSignatures, _sigCard)

            ' -- integration & updates
            _integCard = New CatCard With {.Title = "Integration & updates", .IconName = "link"}
            host.Controls.Add(_integCard)

            _shellMenu = New CatToggle("Add a right-click 'Scan with AVAK' entry",
                                       "Registered for the current user only", ShellIntegration.IsRegistered)
            AddHandler _shellMenu.CheckedChanged, Sub()
                                                      Dim r = If(_shellMenu.Checked,
                                                                 ShellIntegration.Register(),
                                                                 ShellIntegration.Unregister())
                                                      Say(r.Message, Not r.Ok)
                                                      _shellMenu.SetCheckedSilently(ShellIntegration.IsRegistered)
                                                  End Sub
            _integCard.Controls.Add(_shellMenu)

            _updateUrl = New CatInput With {
                .Placeholder = "https://example.com/avak/latest.json", .IconName = "download",
                .Value = cfg.UpdateFeedUrl}
            AddHandler _updateUrl.TextCommitted, Sub()
                                                     cfg.UpdateFeedUrl = _updateUrl.Value.Trim()
                                                     cfg.Save()
                                                 End Sub
            _integCard.Controls.Add(_updateUrl)

            _checkAtStart = New CatToggle("Check for updates at startup",
                                          "Only contacts the URL above", cfg.CheckUpdatesOnStart)
            AddHandler _checkAtStart.CheckedChanged, Sub()
                                                         cfg.CheckUpdatesOnStart = _checkAtStart.Checked
                                                         cfg.Save()
                                                     End Sub
            _integCard.Controls.Add(_checkAtStart)

            _checkNow = Btn("Check now", ButtonKind.Secondary, "refresh", AddressOf CheckUpdates, _integCard)
            _updateStatus = Lbl("", "small", ThemeManager.FgMuted, _integCard)
            _updateStatus.Wrap = True
            _updateStatus.VAlign = StringAlignment.Near

            ' -- about
            _aboutCard = New CatCard With {.Title = "About AVAK", .IconName = "info"}
            host.Controls.Add(_aboutCard)
            _aboutText = Lbl("", "small", ThemeManager.FgMuted, _aboutCard)
            _aboutText.Wrap = True
            _aboutText.VAlign = StringAlignment.Near
            _openData = Btn("Open data folder", ButtonKind.Ghost, "folder",
                            Sub()
                                Try
                                    Process.Start(New ProcessStartInfo(AppPaths.Root) With {.UseShellExecute = True})
                                Catch
                                End Try
                            End Sub, _aboutCard)
            _resetAll = Btn("Reset settings", ButtonKind.Danger, "rotate", AddressOf ResetAll, _aboutCard)
        End Sub

        Public Overrides Sub OnActivated()
            RefreshExclusions()
            RefreshSignatures()
            RefreshAbout()
            _autoStart.SetCheckedSilently(StartupManager.IsSelfAutoStart())
            _shellMenu.SetCheckedSilently(ShellIntegration.IsRegistered)
        End Sub

        ' -- updates ----------------------------------------------------------

        Private Async Sub CheckUpdates(sender As Object, e As EventArgs)
            Dim cfg = AppSettings.Current
            cfg.UpdateFeedUrl = _updateUrl.Value.Trim()
            cfg.Save()

            If String.IsNullOrWhiteSpace(cfg.UpdateFeedUrl) Then
                _updateStatus.SetText("No update feed configured. AVAK never contacts the network on its own.")
                Return
            End If

            _checkNow.Busy = True
            Try
                Dim res = Await UpdateChecker.CheckAsync(cfg.UpdateFeedUrl)
                cfg.LastUpdateCheckUtc = DateTime.UtcNow
                cfg.Save()
                _updateStatus.SetText(res.Message)
                If res.UpdateAvailable AndAlso res.Release IsNot Nothing Then
                    If Ask("Update available", res.Message & Environment.NewLine & Environment.NewLine &
                           If(String.IsNullOrWhiteSpace(res.Release.Notes), "", res.Release.Notes),
                           "Open download page") Then
                        UpdateChecker.OpenDownload(res.Release)
                    End If
                End If
            Finally
                _checkNow.Busy = False
            End Try
        End Sub

        ' -- appearance -------------------------------------------------------

        Private Sub ApplyLook(sender As Object, e As EventArgs)
            Dim cfg = AppSettings.Current
            cfg.Flavor = CType(Math.Max(0, _flavor.SelectedIndex), Flavor)
            cfg.AccentName = _accent.SelectedItem
            cfg.CornerRadius = {3, 12, 18}(Math.Max(0, Math.Min(2, _radius.SelectedIndex)))
            cfg.Save()
            cfg.ApplyTheme()
            _swatches.Invalidate()
            RaiseEvent ThemeApplied()
        End Sub

        Private Sub PaintSwatches(sender As Object, e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Using b As New SolidBrush(ThemeManager.Card)
                g.FillRectangle(b, _swatches.ClientRectangle)
            End Using
            Dim names = Palette.AccentNames
            Dim size = 22
            Dim gap = 6
            For i = 0 To names.Length - 1
                Dim c = ThemeManager.Colors.Accent(names(i))
                Dim x = i * (size + gap)
                Dim r As New RectangleF(x, 4, size, size)
                Gfx.FillRounded(g, r, 7, c)
                If String.Equals(names(i), ThemeManager.AccentName, StringComparison.OrdinalIgnoreCase) Then
                    Gfx.DrawRounded(g, RectangleF.Inflate(r, 2, 2), 9, ThemeManager.Fg, 1.6F)
                End If
            Next
        End Sub

        ' -- exclusions -------------------------------------------------------

        Private Sub RefreshExclusions()
            _excl.SetRows(AppSettings.Current.ExcludedPaths.Select(
                Function(p) New CatRow(p, p) With {.IconName = "ban"}).ToList())
        End Sub

        Private Sub AddExclusionFolder(sender As Object, e As EventArgs)
            Using dlg As New FolderBrowserDialog() With {.Description = "Folder AVAK should never scan"}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                AddExclusion(dlg.SelectedPath)
            End Using
        End Sub

        Private Sub AddExclusionFile(sender As Object, e As EventArgs)
            Using dlg As New OpenFileDialog() With {.Title = "File AVAK should never scan"}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                AddExclusion(dlg.FileName)
            End Using
        End Sub

        Private Sub AddExclusion(p As String)
            Dim cfg = AppSettings.Current
            If Not cfg.ExcludedPaths.Contains(p, StringComparer.OrdinalIgnoreCase) Then
                cfg.ExcludedPaths.Add(p)
                cfg.Save()
                RefreshExclusions()
            End If
        End Sub

        Private Sub RemoveExclusion(sender As Object, e As EventArgs)
            Dim cfg = AppSettings.Current
            For Each r In _excl.SelectedRows
                cfg.ExcludedPaths.Remove(CStr(r.Tag))
            Next
            cfg.Save()
            RefreshExclusions()
        End Sub

        ' -- signatures -------------------------------------------------------

        Private Sub RefreshSignatures()
            _sigInfo.SetText(
                $"Version {SignatureDatabase.Version}" & Environment.NewLine &
                $"{Fmt.Num(SignatureDatabase.HashCount)} hash signatures, {SignatureDatabase.PatternCount} byte patterns" & Environment.NewLine &
                "Shipped packs: " & Content.RuleStore.BuiltInFolder & Environment.NewLine &
                "Your packs:    " & Content.RuleStore.UserFolder)
        End Sub

        Private Sub ImportSignatures(sender As Object, e As EventArgs)
            Using dlg As New OpenFileDialog() With {.Title = "Import signature database", .Filter = "JSON (*.json)|*.json"}
                If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
                Dim msg = SignatureDatabase.ImportFrom(dlg.FileName)
                RefreshSignatures()
                Say(msg)
            End Using
        End Sub

        Private Async Sub UpdateSignatures(sender As Object, e As EventArgs)
            Dim url = _sigUrl.Value.Trim()
            If String.IsNullOrEmpty(url) Then
                Say("Paste a URL that serves an AVAK signature JSON file.", True)
                Return
            End If
            If Not Ask("Download signatures?",
                       "AVAK will fetch:" & Environment.NewLine & url & Environment.NewLine & Environment.NewLine &
                       "The file replaces your user signature database. Only use sources you trust.",
                       "Download") Then Return

            _sigUpdate.Busy = True
            Try
                Dim msg = Await SignatureDatabase.UpdateFromUrlAsync(url)
                RefreshSignatures()
                Say(msg)
            Finally
                _sigUpdate.Busy = False
            End Try
        End Sub

        ' -- about ------------------------------------------------------------

        Private Sub RefreshAbout()
            Dim asm = Reflection.Assembly.GetExecutingAssembly().GetName()
            _aboutText.SetText(
                $"AVAK {asm.Version}" & Environment.NewLine &
                ".NET " & Environment.Version.ToString() & "  -  " & Environment.OSVersion.VersionString & Environment.NewLine &
                "Running " & If(Elevation.IsAdmin, "as administrator", "as a standard user") & Environment.NewLine &
                Environment.NewLine &
                "Data folder: " & AppPaths.Root & Environment.NewLine &
                "AVAK is a learning-focused security suite. It complements Microsoft Defender rather than replacing it: " &
                "keep Defender enabled for kernel-level and cloud protection.")
        End Sub

        Private Sub ResetAll(sender As Object, e As EventArgs)
            If Not Ask("Reset every setting?",
                       "Theme, exclusions, schedule and behaviour go back to their defaults. " &
                       "Quarantined files and scan history are kept.", "Reset", True) Then Return
            Try
                IO.File.Delete(AppPaths.SettingsFile)
            Catch
            End Try
            Dim fresh = AppSettings.Load()
            fresh.ApplyTheme()
            Say("Settings reset. Some changes apply after a restart.")
            RaiseEvent ThemeApplied()
        End Sub

        ' -- layout -----------------------------------------------------------

        Public Overrides Sub Relayout()
            If _scroll Is Nothing Then Return
            _scroll.SetBounds(0, 0, Width, Height)

            Dim w = Math.Max(560, Width - Pad * 2 - 14)
            Dim colW = If(w > 900, (w - 20) \ 2, w)
            Dim twoCol = w > 900

            Dim yL = Pad
            Dim yR = Pad
            Dim xL = Pad
            Dim xR = If(twoCol, Pad + colW + 20, Pad)

            ' appearance (left)
            _lookCard.SetBounds(xL, yL, colW, 250)
            Dim t1 = _lookCard.ContentTop
            _flavor.SetBounds(16, t1 - 4, colW - 32, 38)
            _accent.SetBounds(16, t1 + 42, colW - 32, 38)
            _radius.SetBounds(16, t1 + 88, Math.Min(300, colW - 32), 36)
            _anim.SetBounds(16, t1 + 130, colW - 32, 44)
            _swatches.SetBounds(16, t1 + 178, colW - 32, 32)
            yL += 266

            ' behaviour (right or below)
            _behaviourCard.SetBounds(xR, If(twoCol, yR, yL), colW, 378)
            Dim t2 = _behaviourCard.ContentTop
            Dim togs = {_autoStart, _startMin, _minTray, _closeTray, _toasts, _sounds, _confirmDelete}
            For i = 0 To togs.Length - 1
                togs(i).SetBounds(16, t2 - 8 + i * 46, colW - 32, 44)
            Next
            If twoCol Then yR += 394 Else yL += 394

            ' engine (left)
            Dim engY = If(twoCol, yL, yL)
            _engineCard.SetBounds(xL, engY, colW, 268)
            Dim t3 = _engineCard.ContentTop
            _threads.SetBounds(16, t3 - 4, colW - 32, 38)
            _excl.SetBounds(12, t3 + 42, colW - 24, 268 - t3 - 42 - 52)
            _addExcl.SetBounds(12, 268 - 48, 150, 38)
            _addExclFile.SetBounds(170, 268 - 48, 132, 38)
            _delExcl.SetBounds(310, 268 - 48, 108, 38)
            yL = engY + 284

            ' signatures (right or below)
            Dim sigY = If(twoCol, yR, yL)
            _sigCard.SetBounds(xR, sigY, colW, 272)
            Dim t4 = _sigCard.ContentTop
            _sigInfo.SetBounds(16, t4 - 6, colW - 32, 86)
            _sigReload.SetBounds(16, t4 + 82, 118, 36)
            _sigImport.SetBounds(142, t4 + 82, 150, 36)
            _sigUrl.SetBounds(16, t4 + 126, colW - 32, 38)
            _sigUpdate.SetBounds(16, t4 + 170, 190, 38)
            If twoCol Then yR = sigY + 288 Else yL = sigY + 288

            Dim licY = yL
            ' integration (right or below)
            Dim intY = If(twoCol, yR, licY)
            _integCard.SetBounds(xR, intY, colW, 258)
            Dim t7 = _integCard.ContentTop
            _shellMenu.SetBounds(16, t7 - 8, colW - 32, 44)
            _updateUrl.SetBounds(16, t7 + 40, colW - 32, 38)
            _checkAtStart.SetBounds(16, t7 + 82, colW - 32, 44)
            _checkNow.SetBounds(16, t7 + 132, 130, 38)
            _updateStatus.SetBounds(154, t7 + 128, colW - 172, 60)
            If twoCol Then yR = intY + 274 Else yL = intY + 274

            ' about (full width, last)
            Dim aboutY = Math.Max(yL, yR)
            _aboutCard.SetBounds(Pad, aboutY, w, 210)
            Dim t5 = _aboutCard.ContentTop
            _aboutText.SetBounds(16, t5 - 6, w - 32, 108)
            _openData.SetBounds(16, 210 - 50, 176, 38)
            _resetAll.SetBounds(200, 210 - 50, 154, 38)

            _scroll.SetContentHeight(aboutY + 210 + Pad)
        End Sub

    End Class

End Namespace
