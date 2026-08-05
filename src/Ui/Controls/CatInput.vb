Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    ''' <summary>
    ''' Rounded text field. A real <see cref="TextBox"/> is hosted inside so editing,
    ''' selection, IME and clipboard all behave natively; only the chrome is custom.
    ''' </summary>
    Public Class CatInput
        Inherits CatControlBase

        Private ReadOnly _box As TextBox
        Private _focused As Boolean

        Public Property IconName As String = ""
        Public Property Placeholder As String = ""

        Public Event TextCommitted As EventHandler
        Public Event TextChangedEx As EventHandler

        Public Sub New()
            Height = 40
            Width = 280
            _box = New TextBox() With {
                .BorderStyle = BorderStyle.None,
                .BackColor = ThemeManager.Colors.Surface0,
                .ForeColor = ThemeManager.Fg,
                .Font = ThemeManager.Body
            }
            AddHandler _box.GotFocus, Sub()
                                          _focused = True
                                          Invalidate()
                                      End Sub
            AddHandler _box.LostFocus, Sub()
                                           _focused = False
                                           Invalidate()
                                           RaiseEvent TextCommitted(Me, EventArgs.Empty)
                                       End Sub
            AddHandler _box.TextChanged, Sub()
                                             Invalidate()
                                             RaiseEvent TextChangedEx(Me, EventArgs.Empty)
                                         End Sub
            AddHandler _box.KeyDown, Sub(s As Object, e As KeyEventArgs)
                                         If e.KeyCode = Keys.Enter Then
                                             e.SuppressKeyPress = True
                                             RaiseEvent TextCommitted(Me, EventArgs.Empty)
                                         End If
                                     End Sub
            Controls.Add(_box)
        End Sub

        Public Property Value As String
            Get
                Return _box.Text
            End Get
            Set(v As String)
                _box.Text = If(v, "")
            End Set
        End Property

        Public Property PasswordChar As Char
            Get
                Return _box.PasswordChar
            End Get
            Set(v As Char)
                _box.PasswordChar = v
            End Set
        End Property

        Public Property ReadOnlyBox As Boolean
            Get
                Return _box.ReadOnly
            End Get
            Set(v As Boolean)
                _box.ReadOnly = v
            End Set
        End Property

        Public Sub FocusBox()
            _box.Focus()
        End Sub

        Protected Overrides Sub OnThemeChanged()
            MyBase.OnThemeChanged()
            _box.BackColor = ThemeManager.Colors.Surface0
            _box.ForeColor = ThemeManager.Fg
            _box.Font = ThemeManager.Body
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            LayoutBox()
        End Sub

        Private Sub LayoutBox()
            ' Setting Size in the constructor raises OnResize before _box exists.
            If _box Is Nothing Then Return
            Dim left = If(String.IsNullOrEmpty(IconName), 14, 40)
            _box.SetBounds(left, (Height - _box.PreferredHeight) \ 2, Math.Max(10, Width - left - 14), _box.PreferredHeight)
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            LayoutBox()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim r As New RectangleF(0.5F, 0.5F, Width - 1, Height - 1)
            Gfx.FillRounded(g, r, Radius, ThemeManager.Colors.Surface0)
            Gfx.DrawRounded(g, r, Radius, If(_focused, ThemeManager.Accent, ThemeManager.Border),
                            If(_focused, 1.6F, 1.0F))

            If Not String.IsNullOrEmpty(IconName) Then
                Icons.Draw(g, IconName, New RectangleF(13, (Height - 16) / 2.0F, 16, 16),
                           If(_focused, ThemeManager.Accent, ThemeManager.FgDim), 1.9F)
            End If

            If _box.Text.Length = 0 AndAlso Not String.IsNullOrEmpty(Placeholder) Then
                Dim left = If(String.IsNullOrEmpty(IconName), 14, 40)
                Gfx.TextIn(g, Placeholder, ThemeManager.Body, ThemeManager.FgDim,
                           New RectangleF(left, 0, Width - left - 12, Height))
            End If
        End Sub
    End Class

    ''' <summary>Rounded dropdown backed by a popup list (no native combo chrome).</summary>
    Public Class CatCombo
        Inherits CatControlBase

        Private ReadOnly _items As New List(Of String)()
        Private _index As Integer = -1
        Private _open As Boolean
        Private _popup As Form
        Private _hover As Boolean

        Public Event SelectionChanged As EventHandler

        Public Property IconName As String = ""
        Public Property Placeholder As String = "Select..."

        Public Sub New()
            Height = 40
            Width = 220
            Cursor = Cursors.Hand
        End Sub

        Public Sub SetItems(items As IEnumerable(Of String))
            _items.Clear()
            _items.AddRange(items)
            If _index >= _items.Count Then _index = _items.Count - 1
            Invalidate()
        End Sub

        Public ReadOnly Property Items As IReadOnlyList(Of String)
            Get
                Return _items
            End Get
        End Property

        Public Property SelectedIndex As Integer
            Get
                Return _index
            End Get
            Set(v As Integer)
                Dim n = Math.Max(-1, Math.Min(_items.Count - 1, v))
                If n = _index Then Return
                _index = n
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Public Property SelectedItem As String
            Get
                Return If(_index >= 0 AndAlso _index < _items.Count, _items(_index), "")
            End Get
            Set(v As String)
                SelectedIndex = _items.FindIndex(Function(s) String.Equals(s, v, StringComparison.OrdinalIgnoreCase))
            End Set
        End Property

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
            If _open Then ClosePopup() Else OpenPopup()
        End Sub

        Private Sub OpenPopup()
            If _items.Count = 0 Then Return
            Dim rowH = 34
            Dim visible = Math.Min(_items.Count, 9)
            Dim listH = visible * rowH + 12

            Dim list As New ListBox() With {
                .BorderStyle = BorderStyle.None,
                .BackColor = ThemeManager.Colors.Surface0,
                .ForeColor = ThemeManager.Fg,
                .Font = ThemeManager.Body,
                .DrawMode = DrawMode.OwnerDrawFixed,
                .ItemHeight = rowH,
                .IntegralHeight = False,
                .Dock = DockStyle.Fill
            }
            For Each it In _items
                list.Items.Add(it)
            Next
            list.SelectedIndex = Math.Max(0, _index)

            AddHandler list.DrawItem,
                Sub(s As Object, ev As DrawItemEventArgs)
                    If ev.Index < 0 Then Return
                    Dim g = ev.Graphics
                    Gfx.Quality(g)
                    Dim selected = (ev.State And DrawItemState.Selected) = DrawItemState.Selected
                    Using b As New SolidBrush(If(selected, ThemeManager.Alpha(ThemeManager.Accent, 60), ThemeManager.Colors.Surface0))
                        g.FillRectangle(b, ev.Bounds)
                    End Using
                    Gfx.TextIn(g, CStr(list.Items(ev.Index)), ThemeManager.Body,
                               If(selected, ThemeManager.Fg, ThemeManager.FgMuted),
                               New RectangleF(ev.Bounds.X + 14, ev.Bounds.Y, ev.Bounds.Width - 20, ev.Bounds.Height))
                End Sub

            AddHandler list.MouseUp,
                Sub(s As Object, ev As MouseEventArgs)
                    Dim idx = list.IndexFromPoint(ev.Location)
                    If idx >= 0 Then
                        SelectedIndex = idx
                        ClosePopup()
                    End If
                End Sub

            _popup = New Form() With {
                .FormBorderStyle = FormBorderStyle.None,
                .StartPosition = FormStartPosition.Manual,
                .ShowInTaskbar = False,
                .BackColor = ThemeManager.Colors.Surface0,
                .Padding = New Padding(6),
                .Size = New Size(Width, listH),
                .TopMost = True
            }
            _popup.Controls.Add(list)

            Dim screenPt = PointToScreen(New Point(0, Height + 4))
            Dim wa = Screen.FromControl(Me).WorkingArea
            If screenPt.Y + listH > wa.Bottom Then screenPt.Y = PointToScreen(Point.Empty).Y - listH - 4
            _popup.Location = screenPt

            AddHandler _popup.Deactivate, Sub() ClosePopup()
            _open = True
            Invalidate()
            _popup.Show(FindForm())
            list.Focus()
        End Sub

        Private Sub ClosePopup()
            _open = False
            Invalidate()
            If _popup IsNot Nothing Then
                Dim p = _popup
                _popup = Nothing
                Try
                    p.Close()
                    p.Dispose()
                Catch
                End Try
            End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then ClosePopup()
            MyBase.Dispose(disposing)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim r As New RectangleF(0.5F, 0.5F, Width - 1, Height - 1)
            Gfx.FillRounded(g, r, Radius, ThemeManager.Colors.Surface0)
            Gfx.DrawRounded(g, r, Radius,
                            If(_open OrElse _hover, ThemeManager.Accent, ThemeManager.Border),
                            If(_open, 1.6F, 1.0F))

            Dim x As Single = 14
            If Not String.IsNullOrEmpty(IconName) Then
                Icons.Draw(g, IconName, New RectangleF(x, (Height - 16) / 2.0F, 16, 16), ThemeManager.FgDim, 1.9F)
                x += 26
            End If

            Dim txt = If(_index >= 0, SelectedItem, Placeholder)
            Gfx.TextIn(g, txt, ThemeManager.Body, If(_index >= 0, ThemeManager.Fg, ThemeManager.FgDim),
                       New RectangleF(x, 0, Width - x - 34, Height))

            Icons.Draw(g, If(_open, "chevron-up", "chevron-down"),
                       New RectangleF(Width - 28, (Height - 14) / 2.0F, 14, 14), ThemeManager.FgMuted, 2.0F)
        End Sub
    End Class

End Namespace
