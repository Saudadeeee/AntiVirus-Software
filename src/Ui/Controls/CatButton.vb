Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    Public Enum ButtonKind
        Primary = 0
        Secondary = 1
        Ghost = 2
        Danger = 3
        Success = 4
        Warning = 5
    End Enum

    ''' <summary>Rounded button with icon support, hover/press easing and a busy spinner.</summary>
    Public Class CatButton
        Inherits CatControlBase

        Private _pressed As Boolean
        Private _hover As Boolean
        Private _busy As Boolean
        Private _spinTimer As Timer
        Private _spinAngle As Single

        Public Property Kind As ButtonKind = ButtonKind.Primary
        Public Property IconName As String = ""
        Public Property IconOnly As Boolean = False
        Public Property Caption As String = "Button"
        Public Property Tone As Color = Color.Empty
        Public Property IconSize As Integer = 17

        Public Sub New()
            Size = New Size(140, 40)
            Cursor = Cursors.Hand
        End Sub

        Public Sub New(caption As String, kind As ButtonKind, Optional icon As String = "")
            Me.New()
            Me.Caption = caption
            Me.Kind = kind
            Me.IconName = icon
        End Sub

        Public Property Busy As Boolean
            Get
                Return _busy
            End Get
            Set(value As Boolean)
                If _busy = value Then Return
                _busy = value
                If _busy Then
                    If _spinTimer Is Nothing Then
                        _spinTimer = New Timer() With {.Interval = 33}
                        AddHandler _spinTimer.Tick, Sub()
                                                        _spinAngle = (_spinAngle + 22.0F) Mod 360.0F
                                                        Invalidate()
                                                    End Sub
                    End If
                    _spinTimer.Start()
                Else
                    _spinTimer?.Stop()
                End If
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Cursor = If(Enabled, Cursors.Hand, Cursors.Default)
            Invalidate()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _spinTimer IsNot Nothing Then
                _spinTimer.Stop()
                _spinTimer.Dispose()
                _spinTimer = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            AnimateTo(1.0F)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            _pressed = False
            AnimateTo(0.0F)
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            _pressed = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _pressed = False
            Invalidate()
        End Sub

        Private Function BaseTone() As Color
            If Not Tone.IsEmpty Then Return Tone
            Select Case Kind
                Case ButtonKind.Danger : Return ThemeManager.Danger
                Case ButtonKind.Success : Return ThemeManager.Ok
                Case ButtonKind.Warning : Return ThemeManager.Warn
                Case Else : Return ThemeManager.Accent
            End Select
        End Function

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)

            Dim tn = BaseTone()
            Dim r = New RectangleF(0.5F, 0.5F, Width - 1, Height - 1)
            Dim rad = Math.Min(Radius, Height / 2.0F)
            Dim t = Gfx.EaseOut(AnimValue)
            If _pressed Then t = 1.0F

            Dim fill As Color, fg As Color, border As Color = Color.Empty

            Select Case Kind
                Case ButtonKind.Primary, ButtonKind.Danger, ButtonKind.Success, ButtonKind.Warning
                    Dim top = ThemeManager.Mix(tn, Color.White, 0.10F + 0.10F * t)
                    Dim bot = ThemeManager.Mix(tn, Color.Black, If(_pressed, 0.14F, 0.04F))
                    If Not Enabled Then
                        top = ThemeManager.Colors.Surface1
                        bot = ThemeManager.Colors.Surface1
                    End If
                    Gfx.GradientRounded(g, r, rad, top, bot)
                    fg = If(Enabled, ThemeManager.On_(tn), ThemeManager.FgDim)
                    fill = tn

                Case ButtonKind.Secondary
                    fill = ThemeManager.Mix(ThemeManager.Colors.Surface0, ThemeManager.Colors.Surface1, 0.4F + 0.6F * t)
                    If _pressed Then fill = ThemeManager.Colors.Surface2
                    If Not Enabled Then fill = ThemeManager.Colors.Surface0
                    Gfx.FillRounded(g, r, rad, fill)
                    border = ThemeManager.Mix(ThemeManager.Border, tn, t * 0.8F)
                    fg = If(Enabled, ThemeManager.Fg, ThemeManager.FgDim)

                Case ButtonKind.Ghost
                    If t > 0.01F Then Gfx.FillRounded(g, r, rad, ThemeManager.Alpha(tn, CInt(30 * t)))
                    fg = If(Enabled, ThemeManager.Mix(ThemeManager.FgMuted, tn, t), ThemeManager.FgDim)
            End Select

            If Not border.IsEmpty Then Gfx.DrawRounded(g, r, rad, border, 1.0F)

            ' focus ring
            If Focused AndAlso Enabled Then
                Gfx.DrawRounded(g, RectangleF.Inflate(r, -2, -2), Math.Max(0, rad - 2),
                                ThemeManager.Alpha(ThemeManager.Accent, 150), 1.4F)
            End If

            If _busy Then
                DrawSpinner(g, fg)
                Return
            End If

            Dim hasIcon = Not String.IsNullOrEmpty(IconName)
            Dim label = If(IconOnly, "", Caption)

            If String.IsNullOrEmpty(label) Then
                If hasIcon Then
                    Icons.Draw(g, IconName, New RectangleF((Width - IconSize) / 2.0F, (Height - IconSize) / 2.0F,
                                                           IconSize, IconSize), fg, 1.9F)
                End If
                Return
            End If

            Dim tw = Gfx.Measure(g, label, ThemeManager.BodyBold).Width
            Dim total = tw + If(hasIcon, IconSize + 9, 0)
            Dim x = (Width - total) / 2.0F
            If hasIcon Then
                Icons.Draw(g, IconName, New RectangleF(x, (Height - IconSize) / 2.0F, IconSize, IconSize), fg, 1.9F)
                x += IconSize + 9
            End If
            Gfx.TextIn(g, label, ThemeManager.BodyBold, fg, New RectangleF(x, 0, tw + 4, Height))
        End Sub

        Private Sub DrawSpinner(g As Graphics, fg As Color)
            Dim d = Math.Min(18, Height - 14)
            Dim rect As New RectangleF((Width - d) / 2.0F, (Height - d) / 2.0F, d, d)
            Using p As New Pen(ThemeManager.Alpha(fg, 60), 2.4F)
                g.DrawEllipse(p, rect)
            End Using
            Using p As New Pen(fg, 2.4F)
                p.StartCap = Drawing2D.LineCap.Round
                p.EndCap = Drawing2D.LineCap.Round
                g.DrawArc(p, rect, _spinAngle, 100)
            End Using
        End Sub
    End Class

    ''' <summary>Borderless square icon button (title bar, list row actions).</summary>
    Public Class CatIconButton
        Inherits CatControlBase

        Public Property IconName As String = "x"
        Public Property Tone As Color = Color.Empty
        Public Property HoverTone As Color = Color.Empty
        Public Property IconSize As Integer = 16
        Public Property Circular As Boolean = True

        Public Sub New()
            Size = New Size(34, 34)
            Cursor = Cursors.Hand
        End Sub

        Public Sub New(icon As String, Optional tip As String = "")
            Me.New()
            IconName = icon
            If Not String.IsNullOrEmpty(tip) Then
                Dim tt As New ToolTip()
                tt.SetToolTip(Me, tip)
            End If
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            AnimateTo(1.0F)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            AnimateTo(0.0F)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim t = Gfx.EaseOut(AnimValue)
            Dim hv = If(HoverTone.IsEmpty, ThemeManager.Accent, HoverTone)

            If t > 0.01F Then
                Dim r = New RectangleF(0, 0, Width - 1, Height - 1)
                Gfx.FillRounded(g, r, If(Circular, Height / 2.0F, Radius), ThemeManager.Alpha(hv, CInt(42 * t)))
            End If

            Dim baseTone = If(Tone.IsEmpty, ThemeManager.FgMuted, Tone)
            Dim fg = If(Enabled, ThemeManager.Mix(baseTone, hv, t), ThemeManager.FgDim)
            Icons.Draw(g, IconName, New RectangleF((Width - IconSize) / 2.0F, (Height - IconSize) / 2.0F,
                                                   IconSize, IconSize), fg, 1.9F)
        End Sub
    End Class

    ''' <summary>Segmented control / pill tabs.</summary>
    Public Class CatSegment
        Inherits CatControlBase

        Private _items As New List(Of String)()
        Private _selected As Integer = 0
        Private _hoverIndex As Integer = -1

        Public Event SelectionChanged As EventHandler

        Public Sub New()
            Height = 38
            Width = 320
            Cursor = Cursors.Hand
        End Sub

        Public Sub SetItems(ParamArray items As String())
            _items = items.ToList()
            If _selected >= _items.Count Then _selected = 0
            Invalidate()
        End Sub

        Public Property SelectedIndex As Integer
            Get
                Return _selected
            End Get
            Set(value As Integer)
                Dim v = Math.Max(0, Math.Min(_items.Count - 1, value))
                If v = _selected Then Return
                _selected = v
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public ReadOnly Property SelectedText As String
            Get
                Return If(_selected >= 0 AndAlso _selected < _items.Count, _items(_selected), "")
            End Get
        End Property

        Private Function SegWidth() As Single
            Return If(_items.Count = 0, Width, (Width - 8.0F) / _items.Count)
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim idx = CInt(Math.Floor((e.X - 4) / SegWidth()))
            If idx < 0 OrElse idx >= _items.Count Then idx = -1
            If idx <> _hoverIndex Then
                _hoverIndex = idx
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverIndex = -1
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If _hoverIndex >= 0 Then SelectedIndex = _hoverIndex
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim outer = New RectangleF(0, 0, Width - 1, Height - 1)
            Gfx.FillRounded(g, outer, Height / 2.0F, ThemeManager.Colors.Mantle)
            Gfx.DrawRounded(g, outer, Height / 2.0F, ThemeManager.Border, 1.0F)

            Dim w = SegWidth()
            For i = 0 To _items.Count - 1
                Dim r As New RectangleF(4 + i * w, 4, w, Height - 8)
                Dim isSel = (i = _selected)
                If isSel Then
                    Gfx.FillRounded(g, r, r.Height / 2.0F, ThemeManager.Accent)
                ElseIf i = _hoverIndex Then
                    Gfx.FillRounded(g, r, r.Height / 2.0F, ThemeManager.Alpha(ThemeManager.Accent, 34))
                End If
                Dim fg = If(isSel, ThemeManager.On_(ThemeManager.Accent),
                            If(i = _hoverIndex, ThemeManager.Fg, ThemeManager.FgMuted))
                Gfx.TextIn(g, _items(i), If(isSel, ThemeManager.BodyBold, ThemeManager.Body), fg, r,
                           StringAlignment.Center, StringAlignment.Center)
            Next
        End Sub
    End Class

End Namespace
