Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme

Namespace Ui.Controls

    Public Class CatColumn
        Public Property Title As String = ""
        ''' <summary>Fixed pixel width, or 0 to share the remaining space.</summary>
        Public Property Width As Integer = 0
        Public Property Align As StringAlignment = StringAlignment.Near
        Public Property Muted As Boolean = False

        Public Sub New()
        End Sub

        Public Sub New(title As String, width As Integer,
                       Optional align As StringAlignment = StringAlignment.Near,
                       Optional muted As Boolean = False)
            Me.Title = title
            Me.Width = width
            Me.Align = align
            Me.Muted = muted
        End Sub
    End Class

    Public Class CatRow
        Public Property Cells As String()
        Public Property Tag As Object
        Public Property Tone As Color = Color.Empty
        Public Property IconName As String = ""
        Public Property Checked As Boolean = False

        Public Sub New(tag As Object, ParamArray cells As String())
            Me.Tag = tag
            Me.Cells = cells
        End Sub

        Public Function Cell(i As Integer) As String
            If Cells Is Nothing OrElse i < 0 OrElse i >= Cells.Length Then Return ""
            Return If(Cells(i), "")
        End Function
    End Class

    ''' <summary>
    ''' Virtualised, owner-drawn data table: sortable headers, multi-select,
    ''' optional checkboxes, live filtering and a slim custom scrollbar.
    ''' </summary>
    Public Class CatTable
        Inherits CatControlBase

        Private ReadOnly _columns As New List(Of CatColumn)()
        Private ReadOnly _all As New List(Of CatRow)()
        Private ReadOnly _view As New List(Of CatRow)()
        Private ReadOnly _selected As New HashSet(Of Integer)()

        Private _scroll As Integer
        Private _hoverRow As Integer = -1
        Private _hoverHeader As Integer = -1
        Private _sortColumn As Integer = -1
        Private _sortAsc As Boolean = True
        Private _filter As String = ""
        Private _anchor As Integer = -1
        Private _draggingThumb As Boolean
        Private _dragOffset As Integer

        Public Property RowHeight As Integer = 42
        Public Property HeaderHeight As Integer = 36
        Public Property ShowCheckboxes As Boolean = False
        Public Property MultiSelect As Boolean = True
        Public Property EmptyText As String = "Nothing here yet"
        Public Property EmptyIcon As String = "folder"
        Public Property Sortable As Boolean = True

        Public Event SelectionChanged As EventHandler
        Public Event RowActivated As EventHandler
        Public Event CheckedChanged As EventHandler

        Private Const ScrollW As Integer = 9
        Private Const CheckW As Integer = 38

        Public Sub New()
            Size = New Size(600, 320)
            SetStyle(ControlStyles.Selectable, True)
            TabStop = True
        End Sub

        ' -- data -------------------------------------------------------------
        Public Sub SetColumns(ParamArray cols As CatColumn())
            _columns.Clear()
            _columns.AddRange(cols)
            Invalidate()
        End Sub

        Public Sub SetRows(rows As IEnumerable(Of CatRow))
            _all.Clear()
            If rows IsNot Nothing Then _all.AddRange(rows)
            _selected.Clear()
            _anchor = -1
            Rebuild()
            _scroll = 0
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub AddRow(row As CatRow)
            _all.Add(row)
            Rebuild()
        End Sub

        Public Sub ClearRows()
            SetRows(Nothing)
        End Sub

        Public ReadOnly Property RowCount As Integer
            Get
                Return _view.Count
            End Get
        End Property

        Public ReadOnly Property TotalCount As Integer
            Get
                Return _all.Count
            End Get
        End Property

        Public ReadOnly Property VisibleRows As IReadOnlyList(Of CatRow)
            Get
                Return _view
            End Get
        End Property

        Public ReadOnly Property SelectedRows As List(Of CatRow)
            Get
                Return _selected.Where(Function(i) i >= 0 AndAlso i < _view.Count).
                                 OrderBy(Function(i) i).
                                 Select(Function(i) _view(i)).ToList()
            End Get
        End Property

        Public ReadOnly Property CheckedRows As List(Of CatRow)
            Get
                Return _all.Where(Function(r) r.Checked).ToList()
            End Get
        End Property

        Public Sub SetAllChecked(value As Boolean)
            For Each r In _view
                r.Checked = value
            Next
            Invalidate()
            RaiseEvent CheckedChanged(Me, EventArgs.Empty)
        End Sub

        Public Property Filter As String
            Get
                Return _filter
            End Get
            Set(v As String)
                _filter = If(v, "").Trim()
                _selected.Clear()
                Rebuild()
                _scroll = 0
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
            End Set
        End Property

        Private Sub Rebuild()
            _view.Clear()
            If String.IsNullOrEmpty(_filter) Then
                _view.AddRange(_all)
            Else
                Dim f = _filter
                _view.AddRange(_all.Where(Function(r) r.Cells IsNot Nothing AndAlso
                    r.Cells.Any(Function(c) c IsNot Nothing AndAlso
                                c.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)))
            End If

            If _sortColumn >= 0 Then
                Dim idx = _sortColumn
                Dim sorted = _view.OrderBy(Function(r) r.Cell(idx), New NaturalComparer()).ToList()
                If Not _sortAsc Then sorted.Reverse()
                _view.Clear()
                _view.AddRange(sorted)
            End If

            ClampScroll()
            Invalidate()
        End Sub

        ''' <summary>Sorts numbers inside strings numerically ("file10" after "file9").</summary>
        Private Class NaturalComparer
            Implements IComparer(Of String)

            Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
                Dim a = If(x, ""), b = If(y, "")
                Dim da, db As Double
                If Double.TryParse(a.TrimStart("$"c, " "c), da) AndAlso Double.TryParse(b.TrimStart("$"c, " "c), db) Then
                    Return da.CompareTo(db)
                End If
                Return String.Compare(a, b, StringComparison.OrdinalIgnoreCase)
            End Function
        End Class

        ' -- geometry ---------------------------------------------------------
        Private ReadOnly Property BodyHeight As Integer
            Get
                Return Math.Max(0, Height - HeaderHeight)
            End Get
        End Property

        Private ReadOnly Property ContentHeight As Integer
            Get
                Return _view.Count * RowHeight
            End Get
        End Property

        Private ReadOnly Property NeedsScroll As Boolean
            Get
                Return ContentHeight > BodyHeight
            End Get
        End Property

        Private Sub ClampScroll()
            Dim maxScroll = Math.Max(0, ContentHeight - BodyHeight)
            _scroll = Math.Max(0, Math.Min(maxScroll, _scroll))
        End Sub

        Private Function ColumnWidths() As Integer()
            Dim n = _columns.Count
            Dim w(Math.Max(0, n - 1)) As Integer
            If n = 0 Then Return w
            Dim avail = Width - If(NeedsScroll, ScrollW + 4, 0) - If(ShowCheckboxes, CheckW, 0) - 8
            Dim fixedSum = 0
            Dim flexCount = 0
            For i = 0 To n - 1
                If _columns(i).Width > 0 Then
                    fixedSum += _columns(i).Width
                Else
                    flexCount += 1
                End If
            Next
            Dim flexEach = If(flexCount = 0, 0, Math.Max(60, (avail - fixedSum) \ flexCount))
            For i = 0 To n - 1
                w(i) = If(_columns(i).Width > 0, _columns(i).Width, flexEach)
            Next
            Return w
        End Function

        Private Function RowAt(y As Integer) As Integer
            If y < HeaderHeight Then Return -1
            Dim idx = (y - HeaderHeight + _scroll) \ RowHeight
            If idx < 0 OrElse idx >= _view.Count Then Return -1
            Return idx
        End Function

        ' -- interaction ------------------------------------------------------
        Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
            MyBase.OnMouseWheel(e)
            If Not NeedsScroll Then Return
            _scroll -= CInt(e.Delta / 120.0 * RowHeight * 2)
            ClampScroll()
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)

            If _draggingThumb Then
                Dim track = BodyHeight
                Dim thumbH = ThumbHeight()
                Dim newTop = e.Y - HeaderHeight - _dragOffset
                Dim ratio = newTop / CSng(Math.Max(1, track - thumbH))
                _scroll = CInt(ratio * Math.Max(0, ContentHeight - BodyHeight))
                ClampScroll()
                Invalidate()
                Return
            End If

            Dim hr = RowAt(e.Y)
            Dim hh = -1
            If e.Y < HeaderHeight Then hh = HeaderIndexAt(e.X)
            If hr <> _hoverRow OrElse hh <> _hoverHeader Then
                _hoverRow = hr
                _hoverHeader = hh
                Cursor = If(hh >= 0 AndAlso Sortable, Cursors.Hand, Cursors.Default)
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _hoverRow = -1
            _hoverHeader = -1
            Invalidate()
        End Sub

        Private Function HeaderIndexAt(x As Integer) As Integer
            Dim widths = ColumnWidths()
            Dim cx = 4 + If(ShowCheckboxes, CheckW, 0)
            For i = 0 To _columns.Count - 1
                If x >= cx AndAlso x < cx + widths(i) Then Return i
                cx += widths(i)
            Next
            Return -1
        End Function

        Private Function ThumbHeight() As Integer
            If Not NeedsScroll Then Return 0
            Return Math.Max(28, CInt(BodyHeight * (BodyHeight / CSng(ContentHeight))))
        End Function

        Private Function ThumbTop() As Integer
            Dim maxScroll = Math.Max(1, ContentHeight - BodyHeight)
            Return HeaderHeight + CInt((BodyHeight - ThumbHeight()) * (_scroll / CSng(maxScroll)))
        End Function

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()

            If NeedsScroll AndAlso e.X >= Width - ScrollW - 3 Then
                Dim tt = ThumbTop(), th = ThumbHeight()
                If e.Y >= tt AndAlso e.Y <= tt + th Then
                    _draggingThumb = True
                    _dragOffset = e.Y - tt
                Else
                    _scroll = CInt((e.Y - HeaderHeight - th / 2.0F) / Math.Max(1, BodyHeight - th) *
                                   Math.Max(0, ContentHeight - BodyHeight))
                    ClampScroll()
                    Invalidate()
                End If
                Return
            End If

            If e.Y < HeaderHeight Then
                If Sortable Then
                    Dim h = HeaderIndexAt(e.X)
                    If h >= 0 Then
                        If _sortColumn = h Then _sortAsc = Not _sortAsc Else _sortColumn = h : _sortAsc = True
                        Rebuild()
                    End If
                End If
                Return
            End If

            Dim idx = RowAt(e.Y)
            If idx < 0 Then Return

            If ShowCheckboxes AndAlso e.X < CheckW Then
                _view(idx).Checked = Not _view(idx).Checked
                Invalidate()
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
                Return
            End If

            Dim ctrl = (ModifierKeys And Keys.Control) = Keys.Control
            Dim shift = (ModifierKeys And Keys.Shift) = Keys.Shift

            If MultiSelect AndAlso shift AndAlso _anchor >= 0 Then
                _selected.Clear()
                For i = Math.Min(_anchor, idx) To Math.Max(_anchor, idx)
                    _selected.Add(i)
                Next
            ElseIf MultiSelect AndAlso ctrl Then
                If _selected.Contains(idx) Then _selected.Remove(idx) Else _selected.Add(idx)
                _anchor = idx
            Else
                _selected.Clear()
                _selected.Add(idx)
                _anchor = idx
            End If

            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            _draggingThumb = False
        End Sub

        Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
            MyBase.OnMouseDoubleClick(e)
            If e.Y > HeaderHeight AndAlso RowAt(e.Y) >= 0 Then RaiseEvent RowActivated(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData
                Case Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End, Keys.Space
                    Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If _view.Count = 0 Then Return
            Dim cur = If(_selected.Count > 0, _selected.Min(), -1)
            Dim [next] = cur

            Select Case e.KeyCode
                Case Keys.Up : [next] = Math.Max(0, cur - 1)
                Case Keys.Down : [next] = Math.Min(_view.Count - 1, cur + 1)
                Case Keys.PageUp : [next] = Math.Max(0, cur - BodyHeight \ RowHeight)
                Case Keys.PageDown : [next] = Math.Min(_view.Count - 1, cur + BodyHeight \ RowHeight)
                Case Keys.Home : [next] = 0
                Case Keys.End : [next] = _view.Count - 1
                Case Keys.Enter
                    RaiseEvent RowActivated(Me, EventArgs.Empty)
                    Return
                Case Keys.Space
                    If ShowCheckboxes AndAlso cur >= 0 Then
                        _view(cur).Checked = Not _view(cur).Checked
                        Invalidate()
                        RaiseEvent CheckedChanged(Me, EventArgs.Empty)
                    End If
                    Return
                Case Keys.A
                    If (ModifierKeys And Keys.Control) = Keys.Control AndAlso MultiSelect Then
                        _selected.Clear()
                        For i = 0 To _view.Count - 1
                            _selected.Add(i)
                        Next
                        Invalidate()
                        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                    End If
                    Return
                Case Else
                    Return
            End Select

            If [next] < 0 Then [next] = 0
            _selected.Clear()
            _selected.Add([next])
            _anchor = [next]
            EnsureVisible([next])
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub EnsureVisible(index As Integer)
            Dim top = index * RowHeight
            If top < _scroll Then
                _scroll = top
            ElseIf top + RowHeight > _scroll + BodyHeight Then
                _scroll = top + RowHeight - BodyHeight
            End If
            ClampScroll()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            ClampScroll()
        End Sub

        ' -- painting ---------------------------------------------------------
        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            Gfx.Quality(g)
            Dim widths = ColumnWidths()

            ' header
            If HeaderHeight > 0 Then
            Using b As New SolidBrush(ThemeManager.Colors.Mantle)
                g.FillRectangle(b, 0, 0, Width, HeaderHeight)
            End Using
            Using p As New Pen(ThemeManager.Border, 1.0F)
                g.DrawLine(p, 0, HeaderHeight - 0.5F, Width, HeaderHeight - 0.5F)
            End Using

            Dim hx As Single = 4
            If ShowCheckboxes Then
                Dim allChecked = _view.Count > 0 AndAlso _view.All(Function(r) r.Checked)
                DrawCheck(g, New RectangleF(12, (HeaderHeight - 16) / 2.0F, 16, 16), allChecked)
                hx += CheckW
            End If

            For i = 0 To _columns.Count - 1
                Dim r As New RectangleF(hx, 0, widths(i), HeaderHeight)
                Dim isSort = (_sortColumn = i)
                Dim fg = If(isSort, ThemeManager.Accent, If(_hoverHeader = i, ThemeManager.Fg, ThemeManager.FgMuted))
                Dim label = _columns(i).Title
                Gfx.TextIn(g, label, ThemeManager.Small, fg,
                           New RectangleF(r.X + 6, r.Y, r.Width - 24, r.Height), _columns(i).Align)
                If isSort Then
                    Icons.Draw(g, If(_sortAsc, "chevron-up", "chevron-down"),
                               New RectangleF(r.Right - 20, (HeaderHeight - 12) / 2.0F, 12, 12), fg, 2.2F)
                End If
                hx += widths(i)
            Next
            End If

            ' empty state
            If _view.Count = 0 Then
                Dim cy = HeaderHeight + BodyHeight / 2.0F
                Icons.Draw(g, EmptyIcon, New RectangleF(Width / 2.0F - 22, cy - 46, 44, 44),
                           ThemeManager.Alpha(ThemeManager.FgDim, 130), 1.6F)
                Gfx.TextIn(g, EmptyText, ThemeManager.Body, ThemeManager.FgDim,
                           New RectangleF(10, cy + 6, Width - 20, 24), StringAlignment.Center, StringAlignment.Center)
                Return
            End If

            ' rows (virtualised)
            Dim clip = g.Save()
            g.SetClip(New RectangleF(0, HeaderHeight, Width, BodyHeight))

            Dim first = Math.Max(0, _scroll \ RowHeight)
            Dim last = Math.Min(_view.Count - 1, (_scroll + BodyHeight) \ RowHeight)

            For i = first To last
                Dim row = _view(i)
                Dim y = HeaderHeight + i * RowHeight - _scroll
                Dim rr As New RectangleF(0, y, Width, RowHeight)
                Dim isSel = _selected.Contains(i)

                If isSel Then
                    Gfx.FillRounded(g, New RectangleF(3, y + 2, Width - 6 - If(NeedsScroll, ScrollW + 2, 0), RowHeight - 4),
                                    Radius * 0.6F, ThemeManager.Alpha(ThemeManager.Accent, 46))
                ElseIf i = _hoverRow Then
                    Gfx.FillRounded(g, New RectangleF(3, y + 2, Width - 6 - If(NeedsScroll, ScrollW + 2, 0), RowHeight - 4),
                                    Radius * 0.6F, ThemeManager.Alpha(ThemeManager.Colors.Surface1, 150))
                ElseIf i Mod 2 = 1 Then
                    Using b As New SolidBrush(ThemeManager.Alpha(ThemeManager.Colors.Surface0, 70))
                        g.FillRectangle(b, 3, y, Width - 6 - If(NeedsScroll, ScrollW + 2, 0), RowHeight)
                    End Using
                End If

                Dim x As Single = 4
                If ShowCheckboxes Then
                    DrawCheck(g, New RectangleF(12, y + (RowHeight - 16) / 2.0F, 16, 16), row.Checked)
                    x += CheckW
                End If

                For c = 0 To _columns.Count - 1
                    Dim cw = widths(c)
                    Dim cellRect As New RectangleF(x + 6, y, cw - 12, RowHeight)
                    Dim fg = If(_columns(c).Muted, ThemeManager.FgMuted, ThemeManager.Fg)
                    If c = 0 AndAlso Not row.Tone.IsEmpty Then fg = row.Tone

                    If c = 0 AndAlso Not String.IsNullOrEmpty(row.IconName) Then
                        Dim tint = If(row.Tone.IsEmpty, ThemeManager.FgMuted, row.Tone)
                        Icons.Draw(g, row.IconName, New RectangleF(x + 6, y + (RowHeight - 17) / 2.0F, 17, 17), tint, 1.9F)
                        cellRect = New RectangleF(x + 30, y, cw - 36, RowHeight)
                    End If

                    Gfx.TextIn(g, row.Cell(c), ThemeManager.Body, fg, cellRect, _columns(c).Align)
                    x += cw
                Next
            Next

            g.Restore(clip)

            ' scrollbar
            If NeedsScroll Then
                Dim tx = Width - ScrollW - 3
                Gfx.FillRounded(g, New RectangleF(tx, HeaderHeight + 2, ScrollW, BodyHeight - 4),
                                ScrollW / 2.0F, ThemeManager.Alpha(ThemeManager.Colors.Surface0, 160))
                Gfx.FillRounded(g, New RectangleF(tx, ThumbTop(), ScrollW, ThumbHeight()),
                                ScrollW / 2.0F,
                                If(_draggingThumb, ThemeManager.Accent, ThemeManager.Colors.Surface2))
            End If
        End Sub

        Private Sub DrawCheck(g As Graphics, r As RectangleF, checked As Boolean)
            If checked Then
                Gfx.FillRounded(g, r, 4.5F, ThemeManager.Accent)
                Icons.Draw(g, "check", RectangleF.Inflate(r, -3, -3), ThemeManager.On_(ThemeManager.Accent), 2.6F)
            Else
                Gfx.DrawRounded(g, r, 4.5F, ThemeManager.Colors.Overlay0, 1.5F)
            End If
        End Sub
    End Class

End Namespace
