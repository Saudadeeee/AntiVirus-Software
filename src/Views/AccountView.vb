Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Core
Imports AVAK.Security
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Local profile, app lock and lifetime statistics. Nothing is uploaded anywhere.</summary>
    Public Class AccountView
        Inherits ViewBase

        Private _profileCard As CatCard
        Private _avatar As CatControlHost
        Private _name As CatInput
        Private _accent As CatCombo
        Private _saveName As CatButton
        Private _meta As CatLabel

        Private _lockCard As CatCard
        Private _pinState As CatListRow
        Private _setPin As CatButton
        Private _clearPin As CatButton
        Private _lockStart As CatToggle
        Private _lockNow As CatButton

        Private _statsCard As CatCard
        Private _stats As CatStat()

        Private _privacyCard As CatCard
        Private _privacyText As CatLabel

        Public Event LockRequested()

        Public Overrides ReadOnly Property Title As String
            Get
                Return "Account"
            End Get
        End Property

        Public Overrides ReadOnly Property Subtitle As String
            Get
                Return "Local profile - stored on this PC only"
            End Get
        End Property

        Protected Overrides Sub Build()
            Dim p = LocalProfile.Current

            _profileCard = Card("Profile", "user")
            _avatar = New CatControlHost()
            _profileCard.Controls.Add(_avatar)

            _name = New CatInput With {.Placeholder = "Display name", .Value = p.DisplayName, .IconName = "user"}
            _profileCard.Controls.Add(_name)

            _accent = New CatCombo With {.IconName = "palette"}
            _accent.SetItems(Palette.AccentNames)
            _accent.SelectedItem = p.AvatarAccent
            If _accent.SelectedIndex < 0 Then _accent.SelectedIndex = 3
            AddHandler _accent.SelectionChanged, Sub()
                                                     p.AvatarAccent = _accent.SelectedItem
                                                     p.Save()
                                                     _avatar.Invalidate()
                                                 End Sub
            _profileCard.Controls.Add(_accent)

            _saveName = Btn("Save", ButtonKind.Primary, "save",
                            Sub()
                                p.DisplayName = If(String.IsNullOrWhiteSpace(_name.Value), Environment.UserName, _name.Value.Trim())
                                p.Save()
                                _avatar.Invalidate()
                                RefreshMeta()
                                Say("Profile saved.")
                            End Sub, _profileCard)

            _meta = Lbl("", "small", ThemeManager.FgMuted, _profileCard)
            _meta.Wrap = True
            _meta.VAlign = StringAlignment.Near

            ' -- lock
            _lockCard = Card("App lock", "lock")
            _pinState = New CatListRow()
            _lockCard.Controls.Add(_pinState)

            _setPin = Btn("Set PIN", ButtonKind.Primary, "key", Sub() SetPin(), _lockCard)
            _clearPin = Btn("Remove PIN", ButtonKind.Ghost, "unlock", Sub() ClearPin(), _lockCard)
            _lockStart = New CatToggle("Ask for the PIN when AVAK starts", "", p.LockOnStart)
            AddHandler _lockStart.CheckedChanged, Sub()
                                                      If _lockStart.Checked AndAlso Not LocalProfile.Current.HasPin Then
                                                          _lockStart.SetCheckedSilently(False)
                                                          Say("Set a PIN first.", True)
                                                          Return
                                                      End If
                                                      LocalProfile.Current.LockOnStart = _lockStart.Checked
                                                      LocalProfile.Current.Save()
                                                  End Sub
            _lockCard.Controls.Add(_lockStart)
            _lockNow = Btn("Lock now", ButtonKind.Secondary, "logout",
                           Sub()
                               If Not LocalProfile.Current.HasPin Then
                                   Say("Set a PIN first.", True)
                                   Return
                               End If
                               RaiseEvent LockRequested()
                           End Sub, _lockCard)

            ' -- stats
            _statsCard = Card("Your numbers", "activity")
            _stats = {
                New CatStat("Scans run", "scan", ThemeManager.Colors.Sapphire),
                New CatStat("Threats blocked", "shield-alert", ThemeManager.Colors.Red),
                New CatStat("Files last scan", "file", ThemeManager.Colors.Green),
                New CatStat("App launches", "power", ThemeManager.Colors.Mauve)
            }
            For Each s In _stats
                _statsCard.Controls.Add(s)
            Next

            ' -- privacy statement
            _privacyCard = Card("What AVAK stores", "eye")
            _privacyText = Lbl("", "small", ThemeManager.FgMuted, _privacyCard)
            _privacyText.Wrap = True
            _privacyText.VAlign = StringAlignment.Near
            _privacyText.SetText(
                "AVAK has no account server and makes no network calls except the signature update you trigger yourself." &
                Environment.NewLine & Environment.NewLine &
                "Everything lives under " & AppPaths.Root & ":" & Environment.NewLine &
                "  settings.json   - your preferences" & Environment.NewLine &
                "  profile.json    - display name, avatar colour, PIN hash" & Environment.NewLine &
                "  data\           - scan history, activity feed, quarantine index" & Environment.NewLine &
                "  quarantine\     - AES-encrypted copies of detected files" & Environment.NewLine &
                "  logs\           - rolling text logs" & Environment.NewLine & Environment.NewLine &
                "Delete that folder and AVAK forgets everything.")
        End Sub

        Public Overrides Sub OnActivated()
            RefreshMeta()
            RefreshLock()
            Dim cfg = AppSettings.Current
            Dim p = LocalProfile.Current
            _stats(0).SetValue(Fmt.Num(cfg.TotalScans), HistoryStore.All().Count & " report(s) kept")
            _stats(1).SetValue(Fmt.Num(cfg.TotalThreatsBlocked), QuarantineManager.Count & " in quarantine")
            _stats(2).SetValue(Fmt.Num(cfg.LastScanFiles), Fmt.Ago(cfg.LastScanUtc))
            _stats(3).SetValue(Fmt.Num(p.Launches), "since " & p.CreatedAt.ToString("dd MMM yyyy"))
        End Sub

        Private Sub RefreshMeta()
            Dim p = LocalProfile.Current
            _meta.SetText(
                p.DisplayName & Environment.NewLine &
                "Windows user: " & Environment.UserName & " on " & Environment.MachineName & Environment.NewLine &
                "Profile created " & p.CreatedAt.ToString("dd MMM yyyy") & Environment.NewLine &
                "Last opened " & Fmt.Ago(p.LastOpenedAt))
            _avatar.Invalidate()
        End Sub

        Private Sub RefreshLock()
            Dim p = LocalProfile.Current
            _pinState.IconName = If(p.HasPin, "lock", "unlock")
            _pinState.Tone = If(p.HasPin, ThemeManager.Ok, ThemeManager.FgMuted)
            _pinState.Title = If(p.HasPin, "A PIN is set", "No PIN set")
            _pinState.Description = If(p.HasPin,
                "AVAK asks for it when locked. The PIN is hashed with PBKDF2-SHA256 (120k iterations).",
                "Anyone with access to this Windows account can open AVAK.")
            _pinState.TrailingText = If(p.HasPin, "Protected", "Open")
            _pinState.Invalidate()
            _clearPin.Enabled = p.HasPin
            _lockNow.Enabled = p.HasPin
            _lockStart.SetCheckedSilently(p.LockOnStart)
        End Sub

        Private Sub SetPin()
            Dim pin = CatDialog.Prompt(FindForm(), "Set a PIN",
                                       "Choose at least 4 characters. AVAK stores only a PBKDF2 hash.",
                                       "", "PIN", True)
            If pin Is Nothing Then Return
            If pin.Length < 4 Then
                Say("Use at least 4 characters.", True)
                Return
            End If
            Dim again = CatDialog.Prompt(FindForm(), "Confirm PIN", "Type it once more.", "", "PIN", True)
            If again Is Nothing Then Return
            If again <> pin Then
                Say("The two entries do not match.", True)
                Return
            End If
            LocalProfile.Current.SetPin(pin)
            RefreshLock()
            Say("PIN set.")
        End Sub

        Private Sub ClearPin()
            Dim pin = CatDialog.Prompt(FindForm(), "Remove the PIN", "Enter the current PIN.", "", "PIN", True)
            If pin Is Nothing Then Return
            If Not LocalProfile.Current.VerifyPin(pin) Then
                Say("That PIN is not correct.", True)
                Return
            End If
            LocalProfile.Current.SetPin(Nothing)
            RefreshLock()
            Say("PIN removed.")
        End Sub

        Public Overrides Sub Relayout()
            If _profileCard Is Nothing Then Return
            Dim w = Math.Max(600, Width - Pad * 2)
            Dim colW = (w - 20) \ 2

            _profileCard.SetBounds(Pad, Pad, colW, 250)
            Dim t1 = _profileCard.ContentTop
            _avatar.SetBounds(18, t1 - 6, 82, 82)
            _name.SetBounds(114, t1 - 2, colW - 130, 40)
            _accent.SetBounds(114, t1 + 42, colW - 130, 38)
            _saveName.SetBounds(114, t1 + 86, 120, 38)
            _meta.SetBounds(18, t1 + 132, colW - 36, 84)

            _lockCard.SetBounds(Pad + colW + 20, Pad, colW, 250)
            Dim t2 = _lockCard.ContentTop
            _pinState.SetBounds(8, t2 - 8, colW - 16, 58)
            _lockStart.SetBounds(16, t2 + 54, colW - 32, 44)
            _setPin.SetBounds(16, t2 + 106, 128, 38)
            _clearPin.SetBounds(152, t2 + 106, 140, 38)
            _lockNow.SetBounds(300, t2 + 106, 124, 38)

            Dim y = Pad + 266
            _statsCard.SetBounds(Pad, y, w, 178)
            Dim t3 = _statsCard.ContentTop
            Dim q = (w - 32 - 3 * 14) \ 4
            For i = 0 To _stats.Length - 1
                _stats(i).SetBounds(16 + i * (q + 14), t3 - 6, q, 104)
            Next

            Dim y2 = y + 194
            Dim h = Math.Max(180, Height - y2 - Pad)
            _privacyCard.SetBounds(Pad, y2, w, h)
            _privacyText.SetBounds(18, _privacyCard.ContentTop - 6, w - 36, h - _privacyCard.ContentTop)
        End Sub

    End Class

    ''' <summary>Round avatar with the profile initials.</summary>
    Public Class CatControlHost
        Inherits CatControlBase

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim p = LocalProfile.Current
            Dim c = ThemeManager.Colors.Accent(p.AvatarAccent)
            Dim d = Math.Min(Width, Height) - 2
            Dim r As New RectangleF(1, 1, d, d)

            Using br As New Drawing2D.LinearGradientBrush(r, ThemeManager.Mix(c, Color.White, 0.25F), c, 60.0F)
                g.FillEllipse(br, r)
            End Using
            Gfx.TextIn(g, p.Initials(), ThemeManager.GetFont(d * 0.32F, FontStyle.Bold),
                       ThemeManager.On_(c), r, StringAlignment.Center, StringAlignment.Center)
        End Sub
    End Class

End Namespace
