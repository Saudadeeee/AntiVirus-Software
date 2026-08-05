Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme
Imports AVAK.Ui.Controls

Namespace Ui

    Public Enum DialogTone
        Info = 0
        Success = 1
        Warning = 2
        Danger = 3
        Question = 4
    End Enum

    ''' <summary>Catppuccin-styled replacement for MessageBox / InputBox.</summary>
    Public Class CatDialog
        Inherits Form

        Private ReadOnly _tone As DialogTone
        Private ReadOnly _title As String
        Private ReadOnly _message As String
        Private ReadOnly _iconName As String
        Private _input As CatInput

        Private Sub New(title As String, message As String, tn As DialogTone)
            _title = title
            _message = message
            _tone = tn
            _iconName = IconFor(tn)

            FormBorderStyle = FormBorderStyle.None
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            BackColor = ThemeManager.Colors.Mantle
            Font = ThemeManager.Body
            KeyPreview = True
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
            AddHandler KeyDown, Sub(s As Object, e As KeyEventArgs)
                                    If e.KeyCode = Keys.Escape Then
                                        DialogResult = DialogResult.Cancel
                                        Close()
                                    End If
                                End Sub
        End Sub

        Private Shared Function IconFor(t As DialogTone) As String
            Select Case t
                Case DialogTone.Success : Return "check-circle"
                Case DialogTone.Warning : Return "alert"
                Case DialogTone.Danger : Return "shield-alert"
                Case DialogTone.Question : Return "info"
                Case Else : Return "info"
            End Select
        End Function

        Private Function ToneColor() As Color
            Select Case _tone
                Case DialogTone.Success : Return ThemeManager.Ok
                Case DialogTone.Warning : Return ThemeManager.Warn
                Case DialogTone.Danger : Return ThemeManager.Danger
                Case DialogTone.Question : Return ThemeManager.Accent
                Case Else : Return ThemeManager.Info
            End Select
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim r As New RectangleF(0, 0, Width, Height)
            Gfx.FillRounded(g, r, 16, ThemeManager.Colors.Mantle)
            Gfx.DrawRounded(g, New RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 16, ThemeManager.Border, 1.2F)

            Dim tn = ToneColor()
            Dim box As New RectangleF(26, 26, 42, 42)
            Gfx.FillRounded(g, box, 12, ThemeManager.Alpha(tn, 42))
            Icons.Draw(g, _iconName, RectangleF.Inflate(box, -10, -10), tn, 2.0F)

            Gfx.TextIn(g, _title, ThemeManager.Title, ThemeManager.Fg, New RectangleF(82, 24, Width - 108, 28))
            Gfx.TextIn(g, _message, ThemeManager.Body, ThemeManager.FgMuted,
                       New RectangleF(82, 54, Width - 108, Height - 130),
                       StringAlignment.Near, StringAlignment.Near, StringTrimming.Word, True)
        End Sub

        Protected Overrides Sub OnShown(e As EventArgs)
            MyBase.OnShown(e)
            Region = New Region(Gfx.RoundedPath(New RectangleF(0, 0, Width, Height), 16))
        End Sub

        ' -- factories --------------------------------------------------------

        Private Shared Function MeasureHeight(message As String, width As Integer) As Integer
            Using bmp As New Bitmap(1, 1)
                Using g = Graphics.FromImage(bmp)
                    Dim sz = g.MeasureString(message, ThemeManager.Body, width - 108)
                    Return Math.Max(180, CInt(sz.Height) + 150)
                End Using
            End Using
        End Function

        Public Shared Sub Alert(owner As IWin32Window, title As String, message As String,
                               Optional tn As DialogTone = DialogTone.Info)
            Dim w = 470
            Using d As New CatDialog(title, message, tn)
                d.Size = New Size(w, MeasureHeight(message, w))
                Dim ok As New CatButton("OK", ButtonKind.Primary) With {.Width = 110, .Height = 40}
                ok.Location = New Point(d.Width - 110 - 26, d.Height - 40 - 24)
                AddHandler ok.Click, Sub()
                                         d.DialogResult = DialogResult.OK
                                         d.Close()
                                     End Sub
                d.Controls.Add(ok)
                d.AcceptButton = Nothing
                d.ShowDialog(owner)
            End Using
        End Sub

        Public Shared Function Confirm(owner As IWin32Window, title As String, message As String,
                                       Optional confirmText As String = "Confirm",
                                       Optional tn As DialogTone = DialogTone.Question,
                                       Optional destructive As Boolean = False) As Boolean
            Dim w = 480
            Using d As New CatDialog(title, message, tn)
                d.Size = New Size(w, MeasureHeight(message, w))

                Dim yes As New CatButton(confirmText,
                    If(destructive, ButtonKind.Danger, ButtonKind.Primary)) With {.Width = 132, .Height = 40}
                Dim no As New CatButton("Cancel", ButtonKind.Secondary) With {.Width = 110, .Height = 40}

                yes.Location = New Point(d.Width - 132 - 26, d.Height - 40 - 24)
                no.Location = New Point(d.Width - 132 - 110 - 36, d.Height - 40 - 24)

                AddHandler yes.Click, Sub()
                                          d.DialogResult = DialogResult.Yes
                                          d.Close()
                                      End Sub
                AddHandler no.Click, Sub()
                                         d.DialogResult = DialogResult.No
                                         d.Close()
                                     End Sub
                d.Controls.Add(yes)
                d.Controls.Add(no)
                Return d.ShowDialog(owner) = DialogResult.Yes
            End Using
        End Function

        ''' <summary>Returns Nothing when cancelled.</summary>
        Public Shared Function Prompt(owner As IWin32Window, title As String, message As String,
                                      Optional initial As String = "",
                                      Optional placeholder As String = "",
                                      Optional password As Boolean = False) As String
            Dim w = 470
            Using d As New CatDialog(title, message, DialogTone.Question)
                d.Size = New Size(w, MeasureHeight(message, w) + 46)

                d._input = New CatInput() With {
                    .Placeholder = placeholder,
                    .Value = initial,
                    .Width = w - 108,
                    .Height = 42
                }
                If password Then d._input.PasswordChar = "*"c
                d._input.Location = New Point(82, d.Height - 40 - 24 - 56)
                d.Controls.Add(d._input)

                Dim ok As New CatButton("OK", ButtonKind.Primary) With {.Width = 110, .Height = 40}
                Dim no As New CatButton("Cancel", ButtonKind.Secondary) With {.Width = 110, .Height = 40}
                ok.Location = New Point(d.Width - 110 - 26, d.Height - 40 - 24)
                no.Location = New Point(d.Width - 110 - 110 - 36, d.Height - 40 - 24)

                AddHandler ok.Click, Sub()
                                         d.DialogResult = DialogResult.OK
                                         d.Close()
                                     End Sub
                AddHandler no.Click, Sub()
                                         d.DialogResult = DialogResult.Cancel
                                         d.Close()
                                     End Sub
                AddHandler d._input.TextCommitted, Sub()
                                                   End Sub
                d.Controls.Add(ok)
                d.Controls.Add(no)

                AddHandler d.Shown, Sub() d._input.FocusBox()

                If d.ShowDialog(owner) = DialogResult.OK Then Return d._input.Value
                Return Nothing
            End Using
        End Function
    End Class

    ''' <summary>Bottom-right sliding toast. Stacks and auto-dismisses.</summary>
    Public Class Toast
        Inherits Form

        Private Shared ReadOnly Live As New List(Of Toast)()

        Private ReadOnly _title As String
        Private ReadOnly _body As String
        Private ReadOnly _tone As Color
        Private ReadOnly _icon As String
        Private ReadOnly _life As Timer
        Private ReadOnly _slide As Timer
        Private _targetY As Integer
        Private _opacityStep As Double = 0.14

        Private Sub New(title As String, body As String, tn As Color, icon As String, ms As Integer)
            _title = title
            _body = body
            _tone = tn
            _icon = icon

            FormBorderStyle = FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            TopMost = True
            BackColor = ThemeManager.Colors.Crust
            Size = New Size(360, 92)
            Opacity = 0
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.UserPaint, True)

            _life = New Timer() With {.Interval = Math.Max(1200, ms)}
            AddHandler _life.Tick, Sub()
                                       _life.Stop()
                                       _opacityStep = -0.12
                                   End Sub

            _slide = New Timer() With {.Interval = 16}
            AddHandler _slide.Tick, AddressOf Animate

            AddHandler Click, Sub()
                                  _life.Stop()
                                  _opacityStep = -0.2
                              End Sub
        End Sub

        Private Sub Animate(sender As Object, e As EventArgs)
            Dim o = Opacity + _opacityStep
            If o >= 1.0 Then
                o = 1.0
            ElseIf o <= 0.0 Then
                _slide.Stop()
                Close()
                Return
            End If
            Opacity = o
            If Math.Abs(Top - _targetY) > 1 Then Top += CInt((_targetY - Top) * 0.28)
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True
            End Get
        End Property

        Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
            SyncLock Live
                Live.Remove(Me)
            End SyncLock
            _life.Dispose()
            _slide.Dispose()
            Restack()
            MyBase.OnFormClosed(e)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim r As New RectangleF(0, 0, Width, Height)
            Gfx.FillRounded(g, r, 14, ThemeManager.Colors.Surface0)
            Gfx.DrawRounded(g, New RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 14,
                            ThemeManager.Alpha(_tone, 170), 1.4F)

            Dim box As New RectangleF(16, 16, 34, 34)
            Gfx.FillRounded(g, box, 10, ThemeManager.Alpha(_tone, 46))
            Icons.Draw(g, _icon, RectangleF.Inflate(box, -8, -8), _tone, 2.0F)

            Gfx.TextIn(g, _title, ThemeManager.BodyBold, ThemeManager.Fg, New RectangleF(62, 15, Width - 78, 20))
            Gfx.TextIn(g, _body, ThemeManager.Small, ThemeManager.FgMuted,
                       New RectangleF(62, 36, Width - 78, 42),
                       StringAlignment.Near, StringAlignment.Near, StringTrimming.Word, True)
        End Sub

        Private Shared Sub Restack()
            SyncLock Live
                Dim wa = Screen.PrimaryScreen.WorkingArea
                Dim y = wa.Bottom - 18
                For i = Live.Count - 1 To 0 Step -1
                    y -= Live(i).Height + 10
                    Live(i)._targetY = y
                Next
            End SyncLock
        End Sub

        Public Shared Sub Notify(title As String, body As String, tn As Color,
                                 Optional icon As String = "info", Optional ms As Integer = 4200)
            Try
                If Not Core.AppSettings.Current.ShowToasts Then Return
            Catch
            End Try

            Dim act As Action =
                Sub()
                    Dim t As New Toast(title, body, tn, icon, ms)
                    SyncLock Live
                        While Live.Count >= 4
                            Live(0).Close()
                        End While
                        Live.Add(t)
                    End SyncLock
                    Dim wa = Screen.PrimaryScreen.WorkingArea
                    t.Left = wa.Right - t.Width - 18
                    t.Top = wa.Bottom
                    Restack()
                    t.Show()
                    t._slide.Start()
                    t._life.Start()
                End Sub

            Dim main = Application.OpenForms.Cast(Of Form)().FirstOrDefault()
            If main IsNot Nothing AndAlso main.InvokeRequired Then
                Try
                    main.BeginInvoke(act)
                Catch
                End Try
            Else
                act()
            End If
        End Sub

        Public Shared Sub Ok(title As String, body As String)
            Core.Sfx.Success()
            Notify(title, body, ThemeManager.Ok, "check-circle")
        End Sub

        Public Shared Sub Info(title As String, body As String)
            Notify(title, body, ThemeManager.Info, "info")
        End Sub

        Public Shared Sub Warn(title As String, body As String)
            Core.Sfx.Warning()
            Notify(title, body, ThemeManager.Warn, "alert")
        End Sub

        Public Shared Sub Danger(title As String, body As String)
            Core.Sfx.Threat()
            Notify(title, body, ThemeManager.Danger, "shield-alert", 6500)
        End Sub
    End Class

End Namespace
