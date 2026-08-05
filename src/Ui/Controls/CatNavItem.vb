Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    ''' <summary>Sidebar entry: icon, label, active rail, optional badge counter.</summary>
    Public Class CatNavItem
        Inherits CatControlBase

        Private _active As Boolean
        Private _rail As Single

        Public Property Caption As String = ""
        Public Property IconName As String = "shield"
        Public Property Key As String = ""
        Public Property BadgeCount As Integer = 0
        Public Property BadgeTone As Color = Color.Empty
        Public Property Compact As Boolean = False

        Public Sub New()
            Size = New Size(196, 44)
            Cursor = Cursors.Hand
        End Sub

        Public Sub New(key As String, caption As String, icon As String)
            Me.New()
            Me.Key = key
            Me.Caption = caption
            Me.IconName = icon
        End Sub

        Public Property Active As Boolean
            Get
                Return _active
            End Get
            Set(value As Boolean)
                If _active = value Then Return
                _active = value
                AnimateTo(If(value, 1.0F, 0.0F), 0.22F)
            End Set
        End Property

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _rail = 1.0F
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _rail = 0.0F
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)

            Dim t = Gfx.EaseOut(AnimValue)
            Dim accent = ThemeManager.Accent
            Dim r As New RectangleF(6, 2, Width - 12, Height - 4)

            If _active Then
                Gfx.FillRounded(g, r, Radius, ThemeManager.Alpha(accent, 40))
            ElseIf _rail > 0 Then
                Gfx.FillRounded(g, r, Radius, ThemeManager.Alpha(ThemeManager.Colors.Surface0, 190))
            End If

            ' active rail on the left
            If t > 0.02F Then
                Dim railH = (Height - 16) * t
                Gfx.FillRounded(g, New RectangleF(0, (Height - railH) / 2.0F, 4, railH), 2.0F, accent)
            End If

            Dim fg = If(_active, accent, If(_rail > 0, ThemeManager.Fg, ThemeManager.FgMuted))
            Dim iconBox As New RectangleF(If(Compact, (Width - 20) / 2.0F, 18.0F), (Height - 20) / 2.0F, 20, 20)
            Icons.Draw(g, IconName, iconBox, fg, If(_active, 2.1F, 1.9F))

            If Not Compact Then
                Dim right = If(BadgeCount > 0, Width - 46.0F, Width - 20.0F)
                Gfx.TextIn(g, Caption, If(_active, ThemeManager.BodyBold, ThemeManager.Body), fg,
                           New RectangleF(50, 0, right - 50, Height))
            End If

            If BadgeCount > 0 Then
                Dim tn = If(BadgeTone.IsEmpty, ThemeManager.Danger, BadgeTone)
                Dim txt = If(BadgeCount > 99, "99+", BadgeCount.ToString())
                Dim bw = Math.Max(20.0F, Gfx.Measure(g, txt, ThemeManager.Small).Width + 14)
                Dim br As New RectangleF(Width - bw - 14, (Height - 19) / 2.0F, bw, 19)
                Gfx.FillRounded(g, br, 9.5F, tn)
                Gfx.TextIn(g, txt, ThemeManager.Small, ThemeManager.On_(tn), br,
                           StringAlignment.Center, StringAlignment.Center)
            End If
        End Sub
    End Class

    ''' <summary>KPI tile: icon, caption, big value, optional trend line.</summary>
    Public Class CatStat
        Inherits CatControlBase

        Public Property Caption As String = ""
        Public Property Value As String = "-"
        Public Property Hint As String = ""
        Public Property IconName As String = ""
        Public Property Tone As Color = Color.Empty
        Public Property Clickable As Boolean = False

        Public Sub New()
            Size = New Size(210, 104)
        End Sub

        Public Sub New(caption As String, icon As String, tn As Color)
            Me.New()
            Me.Caption = caption
            Me.IconName = icon
            Me.Tone = tn
        End Sub

        Public Overrides Function SurfaceColor() As Color
            Return ThemeManager.Mix(ThemeManager.Card, ThemeManager.CardHover, If(Clickable, AnimValue, 0.0F))
        End Function

        Public Sub SetValue(v As String, Optional hint As String = Nothing)
            Value = v
            If hint IsNot Nothing Then Me.Hint = hint
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            If Clickable Then
                Cursor = Cursors.Hand
                AnimateTo(1.0F)
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If Clickable Then AnimateTo(0.0F)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim tn = If(Tone.IsEmpty, ThemeManager.Accent, Tone)
            Dim r As New RectangleF(0.5F, 0.5F, Width - 1, Height - 1)

            Dim fill = ThemeManager.Mix(ThemeManager.Card, ThemeManager.CardHover, If(Clickable, AnimValue, 0.0F))
            Gfx.FillRounded(g, r, Radius, fill)
            Gfx.DrawRounded(g, r, Radius, ThemeManager.Mix(ThemeManager.Border, tn, 0.25F + 0.4F * AnimValue), 1.0F)

            If Not String.IsNullOrEmpty(IconName) Then
                Dim box As New RectangleF(Width - 46, 14, 32, 32)
                Gfx.FillRounded(g, box, 9.0F, ThemeManager.Alpha(tn, 38))
                Icons.Draw(g, IconName, RectangleF.Inflate(box, -8, -8), tn, 2.0F)
            End If

            Gfx.TextIn(g, Caption, ThemeManager.Small, ThemeManager.FgMuted,
                       New RectangleF(16, 14, Width - 66, 18))
            Gfx.TextIn(g, Value, ThemeManager.GetFont(19.0F, FontStyle.Bold), ThemeManager.Fg,
                       New RectangleF(16, 34, Width - 32, 30))
            If Not String.IsNullOrEmpty(Hint) Then
                Gfx.TextIn(g, Hint, ThemeManager.Small, ThemeManager.FgDim,
                           New RectangleF(16, Height - 28, Width - 32, 18))
            End If
        End Sub
    End Class

    ''' <summary>Icon + title + description row used in checklists and result panes.</summary>
    Public Class CatListRow
        Inherits CatControlBase

        Public Property IconName As String = "check-circle"
        Public Property Tone As Color = Color.Empty
        Public Property Title As String = ""
        Public Property Description As String = ""
        Public Property TrailingText As String = ""
        Public Property Clickable As Boolean = False

        Public Sub New()
            Height = 56
            Width = 420
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            If Clickable Then
                Cursor = Cursors.Hand
                AnimateTo(1.0F)
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If Clickable Then AnimateTo(0.0F)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim tn = If(Tone.IsEmpty, ThemeManager.Accent, Tone)

            If AnimValue > 0.01F Then
                Gfx.FillRounded(g, New RectangleF(0, 0, Width - 1, Height - 1), Radius,
                                ThemeManager.Alpha(ThemeManager.Colors.Surface1, CInt(150 * AnimValue)))
            End If

            Dim box As New RectangleF(12, (Height - 30) / 2.0F, 30, 30)
            Gfx.FillRounded(g, box, 9.0F, ThemeManager.Alpha(tn, 40))
            Icons.Draw(g, IconName, RectangleF.Inflate(box, -7, -7), tn, 2.0F)

            Dim trailW = If(String.IsNullOrEmpty(TrailingText), 0.0F,
                            Gfx.Measure(g, TrailingText, ThemeManager.Small).Width + 18)
            Dim tw = Width - 56 - trailW - 12

            If String.IsNullOrEmpty(Description) Then
                Gfx.TextIn(g, Title, ThemeManager.Body, ThemeManager.Fg, New RectangleF(54, 0, tw, Height))
            Else
                Gfx.TextIn(g, Title, ThemeManager.Body, ThemeManager.Fg, New RectangleF(54, 9, tw, 19))
                Gfx.TextIn(g, Description, ThemeManager.Small, ThemeManager.FgMuted, New RectangleF(54, 28, tw, 18))
            End If

            If trailW > 0 Then
                Gfx.TextIn(g, TrailingText, ThemeManager.Small, tn,
                           New RectangleF(Width - trailW - 12, 0, trailW, Height), StringAlignment.Far)
            End If
        End Sub
    End Class

End Namespace
