Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    ''' <summary>iOS-style animated switch with an optional caption + description.</summary>
    Public Class CatToggle
        Inherits CatControlBase

        Private _checked As Boolean
        Private _knob As Single
        Private _knobTimer As Timer
        Private _hover As Boolean

        Public Event CheckedChanged As EventHandler

        Public Property Caption As String = ""
        Public Property Description As String = ""
        Public Property OnTone As Color = Color.Empty
        Public Property SwitchWidth As Integer = 46
        Public Property SwitchHeight As Integer = 26
        ''' <summary>When True the switch sits on the right edge and the caption on the left.</summary>
        Public Property TrailingSwitch As Boolean = True

        Public Sub New()
            Size = New Size(280, 44)
            Cursor = Cursors.Hand
        End Sub

        Public Sub New(caption As String, description As String, isChecked As Boolean)
            Me.New()
            Me.Caption = caption
            Me.Description = description
            SetCheckedSilently(isChecked)
        End Sub

        Public Property Checked As Boolean
            Get
                Return _checked
            End Get
            Set(value As Boolean)
                If _checked = value Then Return
                _checked = value
                StartKnob()
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End Set
        End Property

        ''' <summary>Changes the visual state without raising <see cref="CheckedChanged"/>.</summary>
        Public Sub SetCheckedSilently(value As Boolean)
            _checked = value
            _knob = If(value, 1.0F, 0.0F)
            Invalidate()
        End Sub

        Private Sub StartKnob()
            If _knobTimer Is Nothing Then
                _knobTimer = New Timer() With {.Interval = 15}
                AddHandler _knobTimer.Tick,
                    Sub()
                        Dim target = If(_checked, 1.0F, 0.0F)
                        Dim d = target - _knob
                        If Math.Abs(d) < 0.02F Then
                            _knob = target
                            _knobTimer.Stop()
                        Else
                            _knob += d * 0.32F
                        End If
                        Invalidate()
                    End Sub
            End If
            Try
                If Core.AppSettings.Current.Animations Then
                    _knobTimer.Start()
                Else
                    _knob = If(_checked, 1.0F, 0.0F)
                    Invalidate()
                End If
            Catch
                _knobTimer.Start()
            End Try
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _knobTimer IsNot Nothing Then
                _knobTimer.Stop()
                _knobTimer.Dispose()
                _knobTimer = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _hover = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hover = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            If Enabled Then Checked = Not Checked
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)

            Dim tn = If(OnTone.IsEmpty, ThemeManager.Accent, OnTone)
            Dim sw = SwitchWidth, sh = SwitchHeight
            Dim sx = If(TrailingSwitch, Width - sw - 2, 0)
            Dim sy = (Height - sh) / 2.0F

            If _hover AndAlso Enabled Then
                Gfx.FillRounded(g, New RectangleF(0, 0, Width - 1, Height - 1), Radius,
                                ThemeManager.Alpha(ThemeManager.Colors.Surface1, 90))
            End If

            ' track
            Dim off = ThemeManager.Colors.Surface2
            Dim track = ThemeManager.Mix(off, tn, _knob)
            If Not Enabled Then track = ThemeManager.Colors.Surface1
            Gfx.FillRounded(g, New RectangleF(sx, sy, sw, sh), sh / 2.0F, track)

            ' knob
            Dim pad As Single = 3
            Dim kd = sh - pad * 2
            Dim kx = sx + pad + (sw - kd - pad * 2) * Gfx.EaseOut(_knob)
            Using b As New SolidBrush(If(Enabled, Color.White, ThemeManager.Colors.Overlay0))
                g.FillEllipse(b, kx, sy + pad, kd, kd)
            End Using

            ' caption + description
            Dim textRight = If(TrailingSwitch, Width - sw - 14, Width)
            Dim textLeft As Single = If(TrailingSwitch, 0, sw + 14)
            Dim tw = textRight - textLeft
            If tw > 20 Then
                Dim fg = If(Enabled, ThemeManager.Fg, ThemeManager.FgDim)
                If String.IsNullOrEmpty(Description) Then
                    Gfx.TextIn(g, Caption, ThemeManager.Body, fg, New RectangleF(textLeft, 0, tw, Height))
                Else
                    Gfx.TextIn(g, Caption, ThemeManager.Body, fg, New RectangleF(textLeft, 4, tw, 19))
                    Gfx.TextIn(g, Description, ThemeManager.Small, ThemeManager.FgMuted,
                               New RectangleF(textLeft, 22, tw, Height - 24))
                End If
            End If
        End Sub
    End Class

End Namespace
