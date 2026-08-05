Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    ''' <summary>
    ''' Vertical scroll host with a slim Catppuccin scrollbar instead of the native one.
    ''' Add children to <see cref="Content"/>; its height defines the scroll range.
    ''' </summary>
    Public Class CatScrollPanel
        Inherits CatControlBase

        Public ReadOnly Content As Panel

        Private _scroll As Integer
        Private _dragging As Boolean
        Private _dragOffset As Integer
        Private _hoverBar As Boolean

        Private Const BarW As Integer = 9

        Public Sub New()
            Content = New Panel() With {.BackColor = Color.Transparent, .AutoSize = False}
            Content.Location = New Point(0, 0)
            Controls.Add(Content)
            AddHandler Content.ControlAdded, Sub() BeginInvoke(New Action(AddressOf Relayout))
            AddHandler Content.Resize, Sub() Invalidate()
        End Sub

        ''' <summary>Set after populating so the scroll range is known.</summary>
        Public Sub SetContentHeight(h As Integer)
            Content.Height = Math.Max(1, h)
            Relayout()
        End Sub

        Public Sub ScrollToTop()
            _scroll = 0
            Relayout()
        End Sub

        Private ReadOnly Property Overflow As Integer
            Get
                Return Math.Max(0, Content.Height - Height)
            End Get
        End Property

        Private ReadOnly Property NeedsBar As Boolean
            Get
                Return Overflow > 0
            End Get
        End Property

        Public Sub Relayout()
            If IsDisposed Then Return
            _scroll = Math.Max(0, Math.Min(Overflow, _scroll))
            Content.Width = Math.Max(1, Width - If(NeedsBar, BarW + 6, 0))
            Content.Top = -_scroll
            Invalidate()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            Relayout()
        End Sub

        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            If Not NeedsBar Then Return
            _scroll -= CInt(e.Delta / 120.0 * 60)
            Relayout()
        End Sub

        Private Function ThumbH() As Integer
            If Not NeedsBar Then Return 0
            Return Math.Max(30, CInt(Height * (Height / CSng(Content.Height))))
        End Function

        Private Function ThumbY() As Integer
            Return CInt((Height - ThumbH()) * (_scroll / CSng(Math.Max(1, Overflow))))
        End Function

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If Not NeedsBar OrElse e.X < Width - BarW - 4 Then Return
            Dim ty = ThumbY(), th = ThumbH()
            If e.Y >= ty AndAlso e.Y <= ty + th Then
                _dragging = True
                _dragOffset = e.Y - ty
            Else
                _scroll = CInt((e.Y - th / 2.0F) / Math.Max(1, Height - th) * Overflow)
                Relayout()
            End If
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim over = NeedsBar AndAlso e.X >= Width - BarW - 4
            If over <> _hoverBar Then
                _hoverBar = over
                Invalidate()
            End If
            If _dragging Then
                Dim th = ThumbH()
                _scroll = CInt((e.Y - _dragOffset) / CSng(Math.Max(1, Height - th)) * Overflow)
                Relayout()
            End If
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _dragging = False
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverBar = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            If Not NeedsBar Then Return
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim x = Width - BarW - 3
            Gfx.FillRounded(g, New RectangleF(x, 2, BarW, Height - 4), BarW / 2.0F,
                            ThemeManager.Alpha(ThemeManager.Colors.Surface0, 140))
            Gfx.FillRounded(g, New RectangleF(x, ThumbY(), BarW, ThumbH()), BarW / 2.0F,
                            If(_dragging OrElse _hoverBar, ThemeManager.Accent, ThemeManager.Colors.Surface2))
        End Sub
    End Class

End Namespace
