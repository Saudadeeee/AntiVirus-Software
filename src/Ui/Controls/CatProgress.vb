Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    ''' <summary>Circular progress with a big centre value - the dashboard hero widget.</summary>
    Public Class CatRing
        Inherits CatControlBase

        Private _value As Single
        Private _shown As Single
        Private _timer As Timer
        Private _sweep As Single

        Public Property Thickness As Single = 12.0F
        Public Property Tone As Color = Color.Empty
        Public Property TrackTone As Color = Color.Empty
        Public Property CenterText As String = ""
        Public Property CenterSub As String = ""
        Public Property CenterIcon As String = ""
        Public Property Indeterminate As Boolean = False
        Public Property ShowPercent As Boolean = True

        Public Sub New()
            Size = New Size(190, 190)
            _timer = New Timer() With {.Interval = 16}
            AddHandler _timer.Tick, AddressOf Tick
        End Sub

        Public Property Value As Single
            Get
                Return _value
            End Get
            Set(v As Single)
                _value = Math.Max(0.0F, Math.Min(100.0F, v))
                EnsureTimer()
            End Set
        End Property

        Public Sub SetValueImmediate(v As Single)
            _value = Math.Max(0.0F, Math.Min(100.0F, v))
            _shown = _value
            Invalidate()
        End Sub

        Private Sub EnsureTimer()
            If Indeterminate OrElse Math.Abs(_shown - _value) > 0.4F Then
                _timer.Start()
            Else
                _shown = _value
                Invalidate()
            End If
        End Sub

        Public Sub StartIndeterminate()
            Indeterminate = True
            _timer.Start()
        End Sub

        Public Sub StopIndeterminate()
            Indeterminate = False
            Invalidate()
        End Sub

        Private Sub Tick(sender As Object, e As EventArgs)
            Dim keepGoing = False
            If Indeterminate Then
                _sweep = (_sweep + 4.5F) Mod 360.0F
                keepGoing = True
            End If
            If Math.Abs(_shown - _value) > 0.4F Then
                _shown += (_value - _shown) * 0.16F
                keepGoing = True
            Else
                _shown = _value
            End If
            Invalidate()
            If Not keepGoing Then _timer.Stop()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _timer IsNot Nothing Then
                _timer.Stop()
                _timer.Dispose()
                _timer = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)

            Dim d = Math.Min(Width, Height) - Thickness - 4
            Dim r As New RectangleF((Width - d) / 2.0F, (Height - d) / 2.0F, d, d)
            Dim tn = If(Tone.IsEmpty, ThemeManager.Accent, Tone)
            Dim track = If(TrackTone.IsEmpty, ThemeManager.Colors.Surface1, TrackTone)

            Using tp As New Pen(track, Thickness)
                tp.StartCap = LineCap.Round
                tp.EndCap = LineCap.Round
                g.DrawEllipse(tp, r)
            End Using

            If Indeterminate Then
                Using bp As New Pen(tn, Thickness)
                    bp.StartCap = LineCap.Round
                    bp.EndCap = LineCap.Round
                    g.DrawArc(bp, r, _sweep, 90)
                End Using
            ElseIf _shown > 0.05F Then
                Dim sweep = 360.0F * _shown / 100.0F
                Using br As New LinearGradientBrush(r, ThemeManager.Mix(tn, Color.White, 0.25F), tn, 45.0F)
                    Using bp As New Pen(br, Thickness)
                        bp.StartCap = LineCap.Round
                        bp.EndCap = LineCap.Round
                        g.DrawArc(bp, r, -90, sweep)
                    End Using
                End Using
            End If

            ' centre content
            Dim cy = Height / 2.0F
            If Not String.IsNullOrEmpty(CenterIcon) Then
                Icons.Draw(g, CenterIcon, New RectangleF(Width / 2.0F - 21, cy - 46, 42, 42), tn, 1.7F)
            End If

            Dim main = If(String.IsNullOrEmpty(CenterText),
                          If(ShowPercent, CInt(_shown).ToString() & "%", ""), CenterText)
            If Not String.IsNullOrEmpty(main) Then
                Gfx.TextIn(g, main, ThemeManager.Display, ThemeManager.Fg,
                           New RectangleF(6, cy - If(String.IsNullOrEmpty(CenterSub), 20, 30) +
                                             If(String.IsNullOrEmpty(CenterIcon), 0.0F, 14.0F),
                                          Width - 12, 40),
                           StringAlignment.Center, StringAlignment.Center)
            End If
            If Not String.IsNullOrEmpty(CenterSub) Then
                Gfx.TextIn(g, CenterSub, ThemeManager.Small, ThemeManager.FgMuted,
                           New RectangleF(10, cy + If(String.IsNullOrEmpty(CenterIcon), 12.0F, 26.0F), Width - 20, 20),
                           StringAlignment.Center, StringAlignment.Center)
            End If
        End Sub
    End Class

    ''' <summary>Rounded linear progress bar with an indeterminate shuttle mode.</summary>
    Public Class CatBar
        Inherits CatControlBase

        Private _value As Single
        Private _shown As Single
        Private _timer As Timer
        Private _shuttle As Single

        Public Property Tone As Color = Color.Empty
        Public Property Indeterminate As Boolean = False
        Public Property BarHeight As Integer = 8

        Public Sub New()
            Size = New Size(260, 8)
            _timer = New Timer() With {.Interval = 16}
            AddHandler _timer.Tick, AddressOf Tick
        End Sub

        Public Property Value As Single
            Get
                Return _value
            End Get
            Set(v As Single)
                _value = Math.Max(0.0F, Math.Min(100.0F, v))
                If Math.Abs(_shown - _value) > 0.3F Then _timer.Start() Else _shown = _value : Invalidate()
            End Set
        End Property

        Public Sub SetIndeterminate(on_ As Boolean)
            Indeterminate = on_
            If on_ Then _timer.Start() Else Invalidate()
        End Sub

        Private Sub Tick(sender As Object, e As EventArgs)
            Dim more = False
            If Indeterminate Then
                _shuttle = (_shuttle + 0.016F) Mod 1.0F
                more = True
            End If
            If Math.Abs(_shown - _value) > 0.3F Then
                _shown += (_value - _shown) * 0.2F
                more = True
            Else
                _shown = _value
            End If
            Invalidate()
            If Not more Then _timer.Stop()
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso _timer IsNot Nothing Then
                _timer.Stop()
                _timer.Dispose()
                _timer = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim h = Math.Min(BarHeight, Height)
            Dim y = (Height - h) / 2.0F
            Dim tn = If(Tone.IsEmpty, ThemeManager.Accent, Tone)

            Gfx.FillRounded(g, New RectangleF(0, y, Width, h), h / 2.0F, ThemeManager.Colors.Surface1)

            If Indeterminate Then
                Dim segW = Width * 0.32F
                Dim x = -segW + (Width + segW) * Gfx.EaseOut(_shuttle)
                Dim clip = g.Save()
                Using gp = Gfx.RoundedPath(New RectangleF(0, y, Width, h), h / 2.0F)
                    g.SetClip(gp)
                    Gfx.FillRounded(g, New RectangleF(x, y, segW, h), h / 2.0F, tn)
                End Using
                g.Restore(clip)
            ElseIf _shown > 0.1F Then
                Dim w = Width * _shown / 100.0F
                Gfx.GradientRounded(g, New RectangleF(0, y, Math.Max(h, w), h), h / 2.0F,
                                    ThemeManager.Mix(tn, Color.White, 0.22F), tn, 0.0F)
            End If
        End Sub
    End Class

    ''' <summary>Rolling sparkline for live CPU / RAM / network series.</summary>
    Public Class CatSpark
        Inherits CatControlBase

        Private ReadOnly _points As New Queue(Of Single)()

        Public Property Capacity As Integer = 90
        Public Property Tone As Color = Color.Empty
        Public Property MaxValue As Single = 100.0F
        Public Property Fill As Boolean = True
        Public Property ShowGrid As Boolean = True
        Public Property Caption As String = ""
        Public Property ValueSuffix As String = "%"

        Public Sub New()
            Size = New Size(300, 90)
        End Sub

        Public Sub Push(v As Single)
            _points.Enqueue(Math.Max(0.0F, v))
            While _points.Count > Capacity
                _points.Dequeue()
            End While
            Invalidate()
        End Sub

        Public Sub Reset()
            _points.Clear()
            Invalidate()
        End Sub

        Public ReadOnly Property Latest As Single
            Get
                Return If(_points.Count = 0, 0.0F, _points.Last())
            End Get
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim tn = If(Tone.IsEmpty, ThemeManager.Accent, Tone)
            Dim padTop As Single = If(String.IsNullOrEmpty(Caption), 4.0F, 22.0F)
            Dim h = Height - padTop - 4
            If h <= 4 Then Return

            If ShowGrid Then
                Using p As New Pen(ThemeManager.Alpha(ThemeManager.Colors.Surface1, 160), 1.0F)
                    p.DashStyle = DashStyle.Dot
                    For i = 1 To 3
                        Dim y = padTop + h * i / 4.0F
                        g.DrawLine(p, 0, y, Width, y)
                    Next
                End Using
            End If

            If Not String.IsNullOrEmpty(Caption) Then
                Gfx.TextIn(g, Caption, ThemeManager.Small, ThemeManager.FgMuted, New RectangleF(0, 0, Width * 0.6F, 18))
                Gfx.TextIn(g, Latest.ToString("0.#") & ValueSuffix, ThemeManager.BodyBold, tn,
                           New RectangleF(Width * 0.4F, 0, Width * 0.6F, 18), StringAlignment.Far)
            End If

            If _points.Count < 2 Then Return
            Dim arr = _points.ToArray()
            Dim n = arr.Length
            Dim dx = Width / CSng(Math.Max(1, Capacity - 1))
            Dim x0 = Width - (n - 1) * dx

            Dim pts(n - 1) As PointF
            For i = 0 To n - 1
                Dim v = Math.Min(arr(i), MaxValue) / Math.Max(0.001F, MaxValue)
                pts(i) = New PointF(x0 + i * dx, padTop + h - h * v)
            Next

            If Fill Then
                Dim poly As New List(Of PointF)(pts)
                poly.Add(New PointF(pts(n - 1).X, padTop + h))
                poly.Add(New PointF(pts(0).X, padTop + h))
                Using br As New LinearGradientBrush(New RectangleF(0, padTop, Math.Max(1, Width), Math.Max(1, h)),
                                                    ThemeManager.Alpha(tn, 90), ThemeManager.Alpha(tn, 6), 90.0F)
                    g.FillPolygon(br, poly.ToArray())
                End Using
            End If

            Using p As New Pen(tn, 2.0F)
                p.LineJoin = LineJoin.Round
                p.StartCap = LineCap.Round
                p.EndCap = LineCap.Round
                g.DrawLines(p, pts)
            End Using

            Using b As New SolidBrush(tn)
                g.FillEllipse(b, pts(n - 1).X - 3, pts(n - 1).Y - 3, 6, 6)
            End Using
        End Sub
    End Class

End Namespace
