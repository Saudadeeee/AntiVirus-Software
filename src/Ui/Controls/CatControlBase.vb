Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    ''' <summary>
    ''' Base for every AVAK control: flicker-free painting, automatic repaint when
    ''' the theme changes, and a cheap eased-animation driver.
    ''' </summary>
    Public MustInherit Class CatControlBase
        Inherits Control

        Private _anim As Timer
        Private _hooked As Boolean

        Protected Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            DoubleBuffered = True
            BackColor = Color.Transparent
            ForeColor = ThemeManager.Fg
            Font = ThemeManager.Body
            AddHandler ThemeManager.Changed, AddressOf ThemeChangedHandler
            _hooked = True
        End Sub

        Private Sub ThemeChangedHandler(sender As Object, e As EventArgs)
            If IsDisposed OrElse Disposing Then Return
            OnThemeChanged()
            If IsHandleCreated Then
                Try
                    BeginInvoke(New Action(Sub() Invalidate()))
                Catch
                End Try
            End If
        End Sub

        Protected Overridable Sub OnThemeChanged()
            ForeColor = ThemeManager.Fg
            Font = ThemeManager.Body
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                If _hooked Then
                    RemoveHandler ThemeManager.Changed, AddressOf ThemeChangedHandler
                    _hooked = False
                End If
                If _anim IsNot Nothing Then
                    _anim.Stop()
                    _anim.Dispose()
                    _anim = Nothing
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub

        ''' <summary>
        ''' The colour this control actually paints, so nested children can fake
        ''' transparency correctly (a label inside a card must use the card's fill,
        ''' not the window background).
        ''' </summary>
        Public Overridable Function SurfaceColor() As Color
            If BackColor.A > 0 AndAlso BackColor <> Color.Transparent Then Return BackColor
            Return ParentBack()
        End Function

        ''' <summary>Nearest painted ancestor colour - used to fake transparency.</summary>
        Protected Function ParentBack() As Color
            Dim p = Parent
            While p IsNot Nothing
                Dim themed = TryCast(p, CatControlBase)
                If themed IsNot Nothing Then Return themed.SurfaceColor()
                If p.BackColor.A > 0 AndAlso p.BackColor <> Color.Transparent Then Return p.BackColor
                p = p.Parent
            End While
            Return ThemeManager.Bg
        End Function

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            Dim c = If(BackColor = Color.Transparent OrElse BackColor.A = 0, ParentBack(), BackColor)
            Using b As New SolidBrush(c)
                e.Graphics.FillRectangle(b, ClientRectangle)
            End Using
        End Sub

        ' -- animation --------------------------------------------------------
        Private _animValue As Single
        Private _animTarget As Single

        ''' <summary>0..1 driver used for hover / press / toggle transitions.</summary>
        Protected ReadOnly Property AnimValue As Single
            Get
                Return _animValue
            End Get
        End Property

        Protected Sub AnimateTo(target As Single, Optional step_ As Single = 0.18F)
            _animTarget = Math.Max(0.0F, Math.Min(1.0F, target))
            If Not AppSettingsAnimationsOn() Then
                _animValue = _animTarget
                Invalidate()
                Return
            End If
            If _anim Is Nothing Then
                _anim = New Timer() With {.Interval = 15}
                AddHandler _anim.Tick,
                    Sub()
                        Dim delta = _animTarget - _animValue
                        If Math.Abs(delta) < 0.01F Then
                            _animValue = _animTarget
                            _anim.Stop()
                        Else
                            _animValue += delta * step_ * 2.2F
                        End If
                        Invalidate()
                    End Sub
            End If
            _anim.Start()
        End Sub

        Private Shared Function AppSettingsAnimationsOn() As Boolean
            Try
                Return Core.AppSettings.Current.Animations
            Catch
                Return True
            End Try
        End Function

        <Browsable(False)>
        Public Property CornerRadius As Integer = -1

        Protected ReadOnly Property Radius As Single
            Get
                Return If(CornerRadius >= 0, CSng(CornerRadius), CSng(ThemeManager.Radius))
            End Get
        End Property

    End Class

    ''' <summary>Flat themed panel (no rounding) - used as a plain layout surface.</summary>
    Public Class CatPanel
        Inherits Panel

        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            DoubleBuffered = True
            BackColor = ThemeManager.Bg
            AddHandler ThemeManager.Changed, AddressOf OnTheme
        End Sub

        Public Property Role As String = "base"

        Private Sub OnTheme(sender As Object, e As EventArgs)
            If IsDisposed Then Return
            BackColor = RoleColor()
            Invalidate()
        End Sub

        Public Function RoleColor() As Color
            Select Case Role
                Case "side" : Return ThemeManager.BgSide
                Case "deep" : Return ThemeManager.BgDeep
                Case "card" : Return ThemeManager.Card
                Case Else : Return ThemeManager.Bg
            End Select
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then RemoveHandler ThemeManager.Changed, AddressOf OnTheme
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>Rounded surface with optional title bar, icon and hover lift.</summary>
    Public Class CatCard
        Inherits CatControlBase

        Private _hover As Boolean

        Public Property Title As String = ""
        Public Property Subtitle As String = ""
        Public Property IconName As String = ""
        Public Property IconTint As Color = Color.Empty
        Public Property Accent As Color = Color.Empty
        Public Property Interactive As Boolean = False
        ''' <summary>Keep room for a subtitle that is filled in later, so layout stays stable.</summary>
        Public Property ReserveSubtitle As Boolean = False
        Public Property Elevated As Boolean = True
        Public Property Padding_ As Integer = 18

        Public Sub New()
            Size = New Size(280, 160)
        End Sub

        Public Overrides Function SurfaceColor() As Color
            Return ThemeManager.Mix(ThemeManager.Card, ThemeManager.CardHover, If(Interactive, AnimValue, 0.0F))
        End Function

        ''' <summary>Client area left for children, after the title block.</summary>
        Public ReadOnly Property ContentTop As Integer
            Get
                If String.IsNullOrEmpty(Title) Then Return Padding_
                Return Padding_ + If(String.IsNullOrEmpty(Subtitle) AndAlso Not ReserveSubtitle, 30, 50)
            End Get
        End Property

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            If Interactive Then
                _hover = True
                AnimateTo(1.0F)
                Cursor = Cursors.Hand
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If Interactive Then
                _hover = False
                AnimateTo(0.0F)
            End If
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)

            Dim r = New RectangleF(1, 1, Width - 2, Height - 2)
            If Elevated Then Gfx.DropShadow(g, RectangleF.Inflate(r, -1, -1), Radius, 5, 26)

            Dim fill = ThemeManager.Mix(ThemeManager.Card, ThemeManager.CardHover, If(Interactive, AnimValue, 0.0F))
            Gfx.FillRounded(g, r, Radius, fill)

            Dim borderCol = If(Accent.IsEmpty,
                               ThemeManager.Mix(ThemeManager.Border, ThemeManager.Accent, If(Interactive, AnimValue * 0.65F, 0.0F)),
                               ThemeManager.Alpha(Accent, 150))
            Gfx.DrawRounded(g, r, Radius, borderCol, 1.0F)

            If Not Accent.IsEmpty Then
                ' left accent rail
                Using gp = Gfx.RoundedPath(New RectangleF(1, 1, Radius * 2, Height - 2), Radius)
                    Dim clip = g.Save()
                    g.SetClip(gp)
                    Using b As New SolidBrush(Accent)
                        g.FillRectangle(b, 1, 1, 4, Height - 2)
                    End Using
                    g.Restore(clip)
                End Using
            End If

            Dim x = Padding_
            If Not String.IsNullOrEmpty(Title) Then
                If Not String.IsNullOrEmpty(IconName) Then
                    Dim tint = If(IconTint.IsEmpty, ThemeManager.Accent, IconTint)
                    Icons.Draw(g, IconName, New RectangleF(x, Padding_ - 1, 19, 19), tint, 1.9F)
                    x += 27
                End If
                Gfx.TextIn(g, Title, ThemeManager.Subtitle, ThemeManager.Fg,
                           New RectangleF(x, Padding_ - 3, Width - x - Padding_, 22))
                If Not String.IsNullOrEmpty(Subtitle) Then
                    Gfx.TextIn(g, Subtitle, ThemeManager.Small, ThemeManager.FgMuted,
                               New RectangleF(Padding_, Padding_ + 19, Width - Padding_ * 2, 18))
                End If
            End If
        End Sub
    End Class

    ''' <summary>Small pill label (status chips, counters).</summary>
    Public Class CatBadge
        Inherits CatControlBase

        Public Property Text_ As String = ""
        Public Property Tone As Color = Color.Empty
        Public Property Solid As Boolean = False
        Public Property IconName As String = ""

        Public Sub New()
            Height = 24
            Width = 90
        End Sub

        Public Sub SetContent(text As String, tn As Color, Optional icon As String = "")
            Text_ = text
            Me.Tone = tn
            IconName = icon
            AutoWidth()
            Invalidate()
        End Sub

        Public Sub AutoWidth()
            Using g = CreateGraphics()
                Dim w = Gfx.Measure(g, Text_, ThemeManager.Small).Width
                Width = CInt(w) + 24 + If(String.IsNullOrEmpty(IconName), 0, 20)
            End Using
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim tn = If(Tone.IsEmpty, ThemeManager.Accent, Tone)
            Dim r = New RectangleF(0, 0, Width - 1, Height - 1)
            Dim rad = Height / 2.0F

            If Solid Then
                Gfx.FillRounded(g, r, rad, tn)
            Else
                Gfx.FillRounded(g, r, rad, ThemeManager.Alpha(tn, 38))
                Gfx.DrawRounded(g, r, rad, ThemeManager.Alpha(tn, 110), 1.0F)
            End If

            Dim fg = If(Solid, ThemeManager.On_(tn), tn)
            Dim x As Single = 10
            If Not String.IsNullOrEmpty(IconName) Then
                Icons.Draw(g, IconName, New RectangleF(x, (Height - 13) / 2.0F, 13, 13), fg, 2.0F)
                x += 17
            End If
            Gfx.TextIn(g, Text_, ThemeManager.Small, fg, New RectangleF(x, 0, Width - x - 8, Height))
        End Sub
    End Class

    ''' <summary>Section heading with a hairline rule.</summary>
    Public Class CatSectionTitle
        Inherits CatControlBase

        Public Property Text_ As String = ""
        Public Property IconName As String = ""

        Public Sub New()
            Height = 30
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim x As Single = 0
            If Not String.IsNullOrEmpty(IconName) Then
                Icons.Draw(g, IconName, New RectangleF(0, (Height - 16) / 2.0F, 16, 16), ThemeManager.Accent, 1.9F)
                x = 23
            End If
            Dim w = Gfx.Measure(g, Text_, ThemeManager.Subtitle).Width
            Gfx.TextIn(g, Text_, ThemeManager.Subtitle, ThemeManager.Fg, New RectangleF(x, 0, w + 6, Height))
            Dim lineX = x + w + 12
            If lineX < Width - 4 Then
                Using p As New Pen(ThemeManager.Border, 1.0F)
                    g.DrawLine(p, lineX, Height / 2.0F, Width, Height / 2.0F)
                End Using
            End If
        End Sub
    End Class

    ''' <summary>Plain themed label that follows the palette.</summary>
    Public Class CatLabel
        Inherits CatControlBase

        Public Property Text_ As String = ""
        Public Property Tone As Color = Color.Empty
        Public Property FontRole As String = "body"     ' display|title|subtitle|body|bold|small
        Public Property Align As StringAlignment = StringAlignment.Near
        Public Property VAlign As StringAlignment = StringAlignment.Center
        Public Property Wrap As Boolean = False

        Public Sub New()
            Height = 20
            Width = 140
        End Sub

        Public Function Fnt() As Font
            Select Case FontRole
                Case "display" : Return ThemeManager.Display
                Case "title" : Return ThemeManager.Title
                Case "subtitle" : Return ThemeManager.Subtitle
                Case "bold" : Return ThemeManager.BodyBold
                Case "small" : Return ThemeManager.Small
                Case Else : Return ThemeManager.Body
            End Select
        End Function

        Public Sub SetText(t As String)
            Text_ = t
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Gfx.Quality(e.Graphics)
            Gfx.TextIn(e.Graphics, Text_, Fnt(), If(Tone.IsEmpty, ThemeManager.Fg, Tone),
                       New RectangleF(0, 0, Width, Height), Align, VAlign,
                       StringTrimming.EllipsisCharacter, Wrap)
        End Sub
    End Class

End Namespace
