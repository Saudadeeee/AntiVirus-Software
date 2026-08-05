Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Text

Namespace Ui

    ''' <summary>
    ''' Minimal SVG path-data parser producing a GDI+ <see cref="GraphicsPath"/>.
    ''' Supports M m L l H h V v C c S s Q q T t A a Z z which covers every glyph in
    ''' <see cref="Icons"/>. Elliptical arcs are flattened to cubic beziers.
    ''' </summary>
    Public NotInheritable Class SvgPath

        Private Sub New()
        End Sub

        Private Shared ReadOnly Cache As New Dictionary(Of String, GraphicsPath)(StringComparer.Ordinal)

        ''' <summary>Parses (and caches) path data drawn in a 24x24 view box.</summary>
        Public Shared Function Parse(d As String) As GraphicsPath
            SyncLock Cache
                Dim cached As GraphicsPath = Nothing
                If Cache.TryGetValue(d, cached) Then Return cached
                Dim gp = Build(d)
                Cache(d) = gp
                Return gp
            End SyncLock
        End Function

        Private Shared Function Build(d As String) As GraphicsPath
            Dim gp As New GraphicsPath(FillMode.Winding)
            Dim toks = Tokenize(d)
            Dim i As Integer = 0
            Dim cur As New PointF(0, 0)
            Dim start As New PointF(0, 0)
            Dim lastCtrl As New PointF(0, 0)
            Dim prevCmd As Char = " "c
            Dim open As Boolean = False

            While i < toks.Count
                Dim cmd As Char
                If toks(i).IsCommand Then
                    cmd = toks(i).Cmd
                    i += 1
                ElseIf prevCmd <> " "c Then
                    ' implicit repeat: after M the repeat is L, after m it is l
                    cmd = If(prevCmd = "M"c, "L"c, If(prevCmd = "m"c, "l"c, prevCmd))
                Else
                    Exit While
                End If

                Dim rel = Char.IsLower(cmd)
                Dim up = Char.ToUpperInvariant(cmd)

                Select Case up
                    Case "M"c
                        Dim x = Num(toks, i) : Dim y = Num(toks, i)
                        cur = Abs(cur, x, y, rel)
                        start = cur
                        open = False
                    Case "L"c
                        Dim x = Num(toks, i) : Dim y = Num(toks, i)
                        Dim p = Abs(cur, x, y, rel)
                        gp.AddLine(cur, p) : open = True : cur = p
                    Case "H"c
                        Dim x = Num(toks, i)
                        Dim p = New PointF(If(rel, cur.X + x, x), cur.Y)
                        gp.AddLine(cur, p) : open = True : cur = p
                    Case "V"c
                        Dim y = Num(toks, i)
                        Dim p = New PointF(cur.X, If(rel, cur.Y + y, y))
                        gp.AddLine(cur, p) : open = True : cur = p
                    Case "C"c
                        Dim c1 = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        Dim c2 = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        Dim p = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        gp.AddBezier(cur, c1, c2, p) : open = True
                        lastCtrl = c2 : cur = p
                    Case "S"c
                        Dim c1 = If(Char.ToUpperInvariant(prevCmd) = "C"c OrElse Char.ToUpperInvariant(prevCmd) = "S"c,
                                    New PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y), cur)
                        Dim c2 = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        Dim p = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        gp.AddBezier(cur, c1, c2, p) : open = True
                        lastCtrl = c2 : cur = p
                    Case "Q"c
                        Dim q = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        Dim p = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        AddQuad(gp, cur, q, p) : open = True
                        lastCtrl = q : cur = p
                    Case "T"c
                        Dim q = If(Char.ToUpperInvariant(prevCmd) = "Q"c OrElse Char.ToUpperInvariant(prevCmd) = "T"c,
                                   New PointF(2 * cur.X - lastCtrl.X, 2 * cur.Y - lastCtrl.Y), cur)
                        Dim p = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        AddQuad(gp, cur, q, p) : open = True
                        lastCtrl = q : cur = p
                    Case "A"c
                        Dim rx = Num(toks, i) : Dim ry = Num(toks, i)
                        Dim rot = Num(toks, i)
                        Dim laf = Num(toks, i) <> 0
                        Dim swf = Num(toks, i) <> 0
                        Dim p = Abs(cur, Num(toks, i), Num(toks, i), rel)
                        AddArc(gp, cur, rx, ry, rot, laf, swf, p) : open = True
                        cur = p
                    Case "Z"c
                        If open Then
                            If Math.Abs(cur.X - start.X) > 0.0001F OrElse Math.Abs(cur.Y - start.Y) > 0.0001F Then
                                gp.AddLine(cur, start)
                            End If
                            gp.CloseFigure()
                        End If
                        cur = start
                        open = False
                End Select

                prevCmd = cmd
            End While

            Return gp
        End Function

        ' -- helpers -----------------------------------------------------------

        Private Structure Tok
            Public IsCommand As Boolean
            Public Cmd As Char
            Public Value As Single
        End Structure

        Private Shared Function Num(t As List(Of Tok), ByRef i As Integer) As Single
            While i < t.Count AndAlso t(i).IsCommand
                i += 1
            End While
            If i >= t.Count Then Return 0
            Dim v = t(i).Value
            i += 1
            Return v
        End Function

        Private Shared Function Abs(cur As PointF, x As Single, y As Single, rel As Boolean) As PointF
            Return If(rel, New PointF(cur.X + x, cur.Y + y), New PointF(x, y))
        End Function

        Private Shared Sub AddQuad(gp As GraphicsPath, p0 As PointF, q As PointF, p1 As PointF)
            ' quadratic -> cubic
            Dim c1 As New PointF(p0.X + 2.0F / 3.0F * (q.X - p0.X), p0.Y + 2.0F / 3.0F * (q.Y - p0.Y))
            Dim c2 As New PointF(p1.X + 2.0F / 3.0F * (q.X - p1.X), p1.Y + 2.0F / 3.0F * (q.Y - p1.Y))
            gp.AddBezier(p0, c1, c2, p1)
        End Sub

        ''' <summary>Endpoint-parameterised elliptical arc -> cubic bezier segments (W3C F.6.5).</summary>
        Private Shared Sub AddArc(gp As GraphicsPath, p0 As PointF, rx As Single, ry As Single,
                                  rotDeg As Single, largeArc As Boolean, sweep As Boolean, p1 As PointF)
            If rx = 0 OrElse ry = 0 Then
                gp.AddLine(p0, p1)
                Return
            End If

            rx = Math.Abs(rx) : ry = Math.Abs(ry)
            Dim phi = rotDeg * Math.PI / 180.0R
            Dim cosP = Math.Cos(phi), sinP = Math.Sin(phi)

            Dim dx2 = (p0.X - p1.X) / 2.0R, dy2 = (p0.Y - p1.Y) / 2.0R
            Dim x1p = cosP * dx2 + sinP * dy2
            Dim y1p = -sinP * dx2 + cosP * dy2

            ' scale radii up if they are too small to span the chord
            Dim lam = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry)
            If lam > 1 Then
                Dim s = Math.Sqrt(lam)
                rx = CSng(rx * s) : ry = CSng(ry * s)
            End If

            Dim sign = If(largeArc <> sweep, 1.0R, -1.0R)
            Dim numer = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p
            Dim denom = rx * rx * y1p * y1p + ry * ry * x1p * x1p
            Dim co = If(denom = 0, 0.0R, sign * Math.Sqrt(Math.Max(0.0R, numer / denom)))

            Dim cxp = co * (rx * y1p / ry)
            Dim cyp = co * -(ry * x1p / rx)
            Dim cx = cosP * cxp - sinP * cyp + (p0.X + p1.X) / 2.0R
            Dim cy = sinP * cxp + cosP * cyp + (p0.Y + p1.Y) / 2.0R

            Dim th1 = AngleOf(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry)
            Dim dth = AngleOf((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry)
            If Not sweep AndAlso dth > 0 Then
                dth -= 2 * Math.PI
            ElseIf sweep AndAlso dth < 0 Then
                dth += 2 * Math.PI
            End If

            Dim segs = Math.Max(1, CInt(Math.Ceiling(Math.Abs(dth) / (Math.PI / 2.0R))))
            Dim delta = dth / segs
            Dim t = 4.0R / 3.0R * Math.Tan(delta / 4.0R)

            Dim prev = p0
            For k = 0 To segs - 1
                Dim a0 = th1 + k * delta
                Dim a1 = a0 + delta
                Dim c0 = Math.Cos(a0), s0 = Math.Sin(a0)
                Dim c1 = Math.Cos(a1), s1 = Math.Sin(a1)

                Dim e1 = EllipsePoint(cx, cy, rx, ry, cosP, sinP, c1, s1)
                Dim d0 = EllipseDeriv(rx, ry, cosP, sinP, c0, s0)
                Dim d1 = EllipseDeriv(rx, ry, cosP, sinP, c1, s1)

                Dim ctl1 As New PointF(CSng(prev.X + t * d0.X), CSng(prev.Y + t * d0.Y))
                Dim ctl2 As New PointF(CSng(e1.X - t * d1.X), CSng(e1.Y - t * d1.Y))
                gp.AddBezier(prev, ctl1, ctl2, e1)
                prev = e1
            Next
        End Sub

        Private Shared Function EllipsePoint(cx As Double, cy As Double, rx As Double, ry As Double,
                                             cosP As Double, sinP As Double, ct As Double, st As Double) As PointF
            Return New PointF(CSng(cx + rx * ct * cosP - ry * st * sinP),
                              CSng(cy + rx * ct * sinP + ry * st * cosP))
        End Function

        Private Shared Function EllipseDeriv(rx As Double, ry As Double, cosP As Double, sinP As Double,
                                             ct As Double, st As Double) As PointF
            Return New PointF(CSng(-rx * st * cosP - ry * ct * sinP),
                              CSng(-rx * st * sinP + ry * ct * cosP))
        End Function

        Private Shared Function AngleOf(ux As Double, uy As Double, vx As Double, vy As Double) As Double
            Dim dot = ux * vx + uy * vy
            Dim len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy))
            If len = 0 Then Return 0
            Dim a = Math.Acos(Math.Max(-1.0R, Math.Min(1.0R, dot / len)))
            If ux * vy - uy * vx < 0 Then a = -a
            Return a
        End Function

        Private Shared Function Tokenize(d As String) As List(Of Tok)
            Dim outp As New List(Of Tok)()
            Dim sb As New StringBuilder()
            Dim i As Integer = 0

            While i < d.Length
                Dim c = d(i)
                If Char.IsLetter(c) Then
                    Flush(sb, outp)
                    outp.Add(New Tok With {.IsCommand = True, .Cmd = c})
                    i += 1
                ElseIf c = ","c OrElse c = " "c OrElse c = vbTab(0) OrElse c = vbCr(0) OrElse c = vbLf(0) Then
                    Flush(sb, outp)
                    i += 1
                ElseIf c = "-"c OrElse c = "+"c Then
                    ' a sign starts a new number unless it follows an exponent marker
                    If sb.Length > 0 AndAlso Not (sb(sb.Length - 1) = "e"c OrElse sb(sb.Length - 1) = "E"c) Then
                        Flush(sb, outp)
                    End If
                    sb.Append(c)
                    i += 1
                ElseIf c = "."c Then
                    ' a second dot inside the same token starts a new number (".5.5")
                    If sb.ToString().Contains("."c) Then Flush(sb, outp)
                    sb.Append(c)
                    i += 1
                Else
                    sb.Append(c)
                    i += 1
                End If
            End While
            Flush(sb, outp)
            Return outp
        End Function

        Private Shared Sub Flush(sb As StringBuilder, outp As List(Of Tok))
            If sb.Length = 0 Then Return
            Dim s = sb.ToString()
            sb.Clear()
            Dim v As Single
            If Single.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then
                outp.Add(New Tok With {.IsCommand = False, .Value = v})
            End If
        End Sub

    End Class

End Namespace
