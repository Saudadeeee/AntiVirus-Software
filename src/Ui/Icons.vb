Imports System.Drawing
Imports System.Drawing.Drawing2D

Namespace Ui

    Public Enum ShapeKind
        PathData
        Circle
        Line
        Rect
        Polyline
        Polygon
        Dot
    End Enum

    Public Structure IconShape
        Public Kind As ShapeKind
        Public Data As String
        Public A As Single, B As Single, C As Single, D As Single, E As Single
    End Structure

    ''' <summary>
    ''' Vector icon set drawn in a 24x24 box with a 2px round-capped stroke
    ''' (Lucide-like geometry, hand-authored so nothing depends on an external font).
    ''' Icons are resolution independent and tinted at draw time.
    ''' </summary>
    Public NotInheritable Class Icons

        Private Sub New()
        End Sub

        ' -- tiny DSL ---------------------------------------------------------
        Private Shared Function P(d As String) As IconShape
            Return New IconShape With {.Kind = ShapeKind.PathData, .Data = d}
        End Function

        Private Shared Function Ci(cx As Single, cy As Single, r As Single) As IconShape
            Return New IconShape With {.Kind = ShapeKind.Circle, .A = cx, .B = cy, .C = r}
        End Function

        Private Shared Function Ln(x1 As Single, y1 As Single, x2 As Single, y2 As Single) As IconShape
            Return New IconShape With {.Kind = ShapeKind.Line, .A = x1, .B = y1, .C = x2, .D = y2}
        End Function

        Private Shared Function Rc(x As Single, y As Single, w As Single, h As Single, r As Single) As IconShape
            Return New IconShape With {.Kind = ShapeKind.Rect, .A = x, .B = y, .C = w, .D = h, .E = r}
        End Function

        Private Shared Function Pl(pts As String) As IconShape
            Return New IconShape With {.Kind = ShapeKind.Polyline, .Data = pts}
        End Function

        Private Shared Function Pg(pts As String) As IconShape
            Return New IconShape With {.Kind = ShapeKind.Polygon, .Data = pts}
        End Function

        Private Shared Function Dt(cx As Single, cy As Single, r As Single) As IconShape
            Return New IconShape With {.Kind = ShapeKind.Dot, .A = cx, .B = cy, .C = r}
        End Function

        Private Const ShieldOutline As String =
            "M12 2.4 L20.2 6.1 L20.2 12 C20.2 17.2 16.8 20.7 12 21.6 C7.2 20.7 3.8 17.2 3.8 12 L3.8 6.1 Z"

        Private Shared ReadOnly Map As New Dictionary(Of String, IconShape()) (StringComparer.OrdinalIgnoreCase) From {
            {"shield", {P(ShieldOutline)}},
            {"shield-check", {P(ShieldOutline), Pl("8.4 12.1, 10.9 14.6, 15.6 9.4")}},
            {"shield-alert", {P(ShieldOutline), Ln(12, 7.6, 12, 13.2), Dt(12, 16.3, 0.95)}},
            {"shield-off", {P(ShieldOutline), Ln(3.2, 3.2, 20.8, 20.8)}},
            {"shield-plus", {P(ShieldOutline), Ln(12, 8.4, 12, 14.4), Ln(9, 11.4, 15, 11.4)}},
            {"search", {Ci(11, 11, 7), Ln(16.1, 16.1, 21, 21)}},
            {"scan", {P("M4 8.5 V6 a2 2 0 0 1 2-2 h2.5"), P("M20 8.5 V6 a2 2 0 0 0-2-2 h-2.5"),
                      P("M4 15.5 V18 a2 2 0 0 0 2 2 h2.5"), P("M20 15.5 V18 a2 2 0 0 1-2 2 h-2.5"),
                      Ln(3, 12, 21, 12)}},
            {"grid", {Rc(3, 3, 7.2, 7.2, 1.6), Rc(13.8, 3, 7.2, 7.2, 1.6),
                      Rc(13.8, 13.8, 7.2, 7.2, 1.6), Rc(3, 13.8, 7.2, 7.2, 1.6)}},
            {"activity", {Pl("2.5 12, 6.5 12, 9.5 4, 14.5 20, 17.5 12, 21.5 12")}},
            {"cpu", {Rc(4, 4, 16, 16, 2.4), Rc(9, 9, 6, 6, 1.2),
                     Ln(9, 1.8, 9, 4), Ln(15, 1.8, 15, 4), Ln(9, 20, 9, 22.2), Ln(15, 20, 15, 22.2),
                     Ln(1.8, 9, 4, 9), Ln(1.8, 15, 4, 15), Ln(20, 9, 22.2, 9), Ln(20, 15, 22.2, 15)}},
            {"memory", {Rc(2.5, 7.5, 19, 8.5, 1.6), Ln(6.5, 16, 6.5, 20.5), Ln(10.5, 16, 10.5, 20.5),
                        Ln(14.5, 16, 14.5, 20.5), Ln(18.5, 16, 18.5, 20.5), Ln(6.5, 10.5, 6.5, 13),
                        Ln(12, 10.5, 12, 13), Ln(17.5, 10.5, 17.5, 13)}},
            {"disk", {Ln(2.5, 12.5, 21.5, 12.5),
                      P("M5.5 5.1 L2.5 12.5 v5.5 a2 2 0 0 0 2 2 h15 a2 2 0 0 0 2-2 v-5.5 L18.5 5.1 A2 2 0 0 0 16.7 4 H7.3 a2 2 0 0 0-1.8 1.1 Z"),
                      Dt(6.6, 16.3, 0.8), Dt(10.2, 16.3, 0.8)}},
            {"globe", {Ci(12, 12, 9.4), Ln(2.6, 12, 21.4, 12),
                       P("M12 2.6 a13.5 9.4 0 0 1 0 18.8 a13.5 9.4 0 0 1 0-18.8")}},
            {"wifi", {P("M2.2 8.7 a14.5 14.5 0 0 1 19.6 0"), P("M5.6 12.5 a9.7 9.7 0 0 1 12.8 0"),
                      P("M9 16.3 a5 5 0 0 1 6 0"), Dt(12, 19.8, 1.1)}},
            {"network", {Rc(8.8, 2, 6.4, 6.4, 1.6), Rc(1.8, 15.6, 6.4, 6.4, 1.6), Rc(15.8, 15.6, 6.4, 6.4, 1.6),
                         Ln(12, 8.4, 12, 12), Ln(5, 15.6, 5, 12), Ln(19, 15.6, 19, 12), Ln(5, 12, 19, 12)}},
            {"lock", {Rc(3.6, 10.4, 16.8, 10.6, 2.2), P("M7.6 10.4 V7 a4.4 4.4 0 0 1 8.8 0 v3.4"), Dt(12, 15.7, 1.05)}},
            {"unlock", {Rc(3.6, 10.4, 16.8, 10.6, 2.2), P("M7.6 10.4 V7 a4.4 4.4 0 0 1 8.3-2"), Dt(12, 15.7, 1.05)}},
            {"user", {Ci(12, 8, 4), P("M4.6 20.8 a7.6 7.6 0 0 1 14.8 0")}},
            {"users", {Ci(9.2, 8, 3.6), P("M2.4 20.6 a6.9 6.9 0 0 1 13.6 0"),
                       P("M16.4 4.9 a3.6 3.6 0 0 1 0 6.3"), P("M18.4 14.2 a6.6 6.6 0 0 1 3.3 6.4")}},
            {"settings", {Ln(3.5, 6.5, 20.5, 6.5), Ln(3.5, 12, 20.5, 12), Ln(3.5, 17.5, 20.5, 17.5),
                          Ci(8.5, 6.5, 2.1), Ci(15.5, 12, 2.1), Ci(7, 17.5, 2.1)}},
            {"bell", {P("M18.4 9.2 a6.4 6.4 0 0 0-12.8 0 c0 6.4-2.4 8.3-2.4 8.3 h17.6 s-2.4-1.9-2.4-8.3"),
                      P("M13.9 20.8 a2.2 2.2 0 0 1-3.8 0")}},
            {"bell-off", {P("M18.4 9.2 a6.4 6.4 0 0 0-12.8 0 c0 6.4-2.4 8.3-2.4 8.3 h17.6 s-2.4-1.9-2.4-8.3"),
                          Ln(3.2, 3.2, 20.8, 20.8)}},
            {"clock", {Ci(12, 12, 9.4), Pl("12 6.4, 12 12.2, 16.2 14.4")}},
            {"history", {P("M3.4 12 a8.9 8.9 0 1 0 2.6-6.3"), Pl("2.4 4.2, 2.4 9.2, 7.4 9.2"),
                         Pl("12 7.6, 12 12.2, 15.6 14")}},
            {"trash", {Ln(3.2, 6, 20.8, 6),
                       P("M8.2 6 V4.6 a1.6 1.6 0 0 1 1.6-1.6 h4.4 a1.6 1.6 0 0 1 1.6 1.6 V6"),
                       P("M5.6 6 l1 13.6 a1.8 1.8 0 0 0 1.8 1.7 h7.2 a1.8 1.8 0 0 0 1.8-1.7 L18.4 6"),
                       Ln(10.2, 10, 10.2, 17.4), Ln(13.8, 10, 13.8, 17.4)}},
            {"folder", {P("M3 7.6 a2 2 0 0 1 2-2 h4.3 l2.1 2.9 H19 a2 2 0 0 1 2 2 V18 a2 2 0 0 1-2 2 H5 a2 2 0 0 1-2-2 Z")}},
            {"file", {P("M13.8 2.6 H7 A2 2 0 0 0 5 4.6 v14.8 a2 2 0 0 0 2 2 h10 a2 2 0 0 0 2-2 V7.8 Z"),
                      Pl("13.8 2.6, 13.8 7.8, 19 7.8")}},
            {"file-alert", {P("M13.8 2.6 H7 A2 2 0 0 0 5 4.6 v14.8 a2 2 0 0 0 2 2 h10 a2 2 0 0 0 2-2 V7.8 Z"),
                            Pl("13.8 2.6, 13.8 7.8, 19 7.8"), Ln(12, 11, 12, 14.6), Dt(12, 17.4, 0.85)}},
            {"check", {Pl("4.5 12.6, 9.6 17.6, 19.6 6.4")}},
            {"check-circle", {Ci(12, 12, 9.4), Pl("7.9 12.2, 10.9 15.2, 16.1 8.9")}},
            {"x", {Ln(6.2, 6.2, 17.8, 17.8), Ln(17.8, 6.2, 6.2, 17.8)}},
            {"x-circle", {Ci(12, 12, 9.4), Ln(8.7, 8.7, 15.3, 15.3), Ln(15.3, 8.7, 8.7, 15.3)}},
            {"alert", {P("M10.3 3.9 L1.8 18 a2 2 0 0 0 1.7 3 h17 a2 2 0 0 0 1.7-3 L13.7 3.9 a2 2 0 0 0-3.4 0 Z"),
                       Ln(12, 9.4, 12, 14), Dt(12, 17.2, 0.85)}},
            {"info", {Ci(12, 12, 9.4), Ln(12, 11.2, 12, 16.4), Dt(12, 8, 0.9)}},
            {"power", {Ln(12, 2.4, 12, 11.8), P("M18.4 6.4 a9 9 0 1 1-12.8 0")}},
            {"play", {Pg("8.2 5.2, 19 12, 8.2 18.8")}},
            {"pause", {Rc(7, 5, 3.4, 14, 1.2), Rc(13.6, 5, 3.4, 14, 1.2)}},
            {"stop", {Rc(6, 6, 12, 12, 2.4)}},
            {"refresh", {P("M20.6 11 A8.7 8.7 0 0 0 6 6.2 L2.8 9.2"), Pl("2.4 4.2, 2.4 9.4, 7.6 9.4"),
                         P("M3.4 13 A8.7 8.7 0 0 0 18 17.8 l3.2-3"), Pl("21.6 19.8, 21.6 14.6, 16.4 14.6")}},
            {"rotate", {Pl("2.4 4.2, 2.4 9.4, 7.6 9.4"), P("M3.1 13 a9 9 0 1 0 2.4-8.8 L2.4 7.2")}},
            {"download", {Ln(12, 2.8, 12, 15.4), Pl("6.6 10.2, 12 15.8, 17.4 10.2"), Ln(4, 20.6, 20, 20.6)}},
            {"upload", {Ln(12, 21.2, 12, 8.6), Pl("6.6 14.2, 12 8.6, 17.4 14.2"), Ln(4, 3.4, 20, 3.4)}},
            {"chevron-right", {Pl("9.6 5, 16.6 12, 9.6 19")}},
            {"chevron-left", {Pl("14.4 5, 7.4 12, 14.4 19")}},
            {"chevron-down", {Pl("5 9.6, 12 16.6, 19 9.6")}},
            {"chevron-up", {Pl("5 14.4, 12 7.4, 19 14.4")}},
            {"plus", {Ln(12, 5, 12, 19), Ln(5, 12, 19, 12)}},
            {"minus", {Ln(5, 12, 19, 12)}},
            {"maximize", {Rc(4.2, 4.2, 15.6, 15.6, 2.4)}},
            {"eye", {P("M1.9 12 C4.1 8 7.7 5.2 12 5.2 C16.3 5.2 19.9 8 22.1 12 C19.9 16 16.3 18.8 12 18.8 C7.7 18.8 4.1 16 1.9 12 Z"),
                     Ci(12, 12, 3.2)}},
            {"eye-off", {P("M1.9 12 C4.1 8 7.7 5.2 12 5.2 C16.3 5.2 19.9 8 22.1 12 C19.9 16 16.3 18.8 12 18.8 C7.7 18.8 4.1 16 1.9 12 Z"),
                         Ln(3.2, 3.2, 20.8, 20.8)}},
            {"database", {P("M3.8 6 a8.2 3.3 0 1 0 16.4 0 a8.2 3.3 0 1 0-16.4 0"),
                          P("M3.8 6 v12 a8.2 3.3 0 0 0 16.4 0 V6"),
                          P("M3.8 12 a8.2 3.3 0 0 0 16.4 0")}},
            {"zap", {Pg("13 2, 3.8 14, 10.8 14, 10 22, 20.2 10, 13.2 10")}},
            {"flame", {P("M12 2.4 c3 4 6 6.3 6 11.1 a6 6 0 0 1-12 0 c0-2.2 1-3.8 2-5.1 .4 1.7 1.4 2.5 2.4 2.5 C11.6 8.5 10.6 5.5 12 2.4 Z")}},
            {"sparkle", {P("M12 2.6 L14 8 L19.4 10 L14 12 L12 17.4 L10 12 L4.6 10 L10 8 Z"),
                         P("M18.4 15 L19.3 17.4 L21.7 18.3 L19.3 19.2 L18.4 21.6 L17.5 19.2 L15.1 18.3 L17.5 17.4 Z"),
                         P("M5 2.6 L5.7 4.5 L7.6 5.2 L5.7 5.9 L5 7.8 L4.3 5.9 L2.4 5.2 L4.3 4.5 Z")}},
            {"key", {Ci(7.6, 15.4, 4), Ln(10.4, 12.6, 20.6, 2.4), Ln(17.6, 5.4, 20.2, 8), Ln(14.6, 8.4, 17.2, 11)}},
            {"logout", {P("M9 21 H5 a2 2 0 0 1-2-2 V5 a2 2 0 0 1 2-2 h4"), Pl("16 16.8, 20.8 12, 16 7.2"),
                        Ln(20.8, 12, 9.4, 12)}},
            {"link", {P("M18 13.4 V19 a2 2 0 0 1-2 2 H5 a2 2 0 0 1-2-2 V8 a2 2 0 0 1 2-2 h5.6"),
                      Pl("15 3, 21 3, 21 9"), Ln(10.2, 13.8, 21, 3)}},
            {"moon", {P("M20.6 14.9 A8.9 8.9 0 0 1 9.1 3.4 A8.9 8.9 0 1 0 20.6 14.9 Z")}},
            {"sun", {Ci(12, 12, 4.2), Ln(12, 1.8, 12, 4), Ln(12, 20, 12, 22.2), Ln(1.8, 12, 4, 12), Ln(20, 12, 22.2, 12),
                     Ln(4.8, 4.8, 6.4, 6.4), Ln(17.6, 17.6, 19.2, 19.2), Ln(19.2, 4.8, 17.6, 6.4), Ln(6.4, 17.6, 4.8, 19.2)}},
            {"palette", {P("M12 2.6 a9.4 9.4 0 0 0 0 18.8 c1.4 0 2.5-1.1 2.5-2.5 0-.6-.2-1.2-.6-1.6-.4-.4-.6-.9-.6-1.5 0-1.4 1.1-2.5 2.5-2.5 h1.8 a4 4 0 0 0 4-4 c0-4.4-4.4-6.7-9.6-6.7 Z"),
                         Dt(7.6, 10.2, 1.1), Dt(11, 6.8, 1.1), Dt(15.4, 7.8, 1.1), Dt(17.4, 11.8, 1.1)}},
            {"gauge", {P("M3.8 17.4 a9.2 9.2 0 1 1 16.4 0"), Ln(12, 14, 16.4, 9.2), Dt(12, 17.2, 1.2)}},
            {"ban", {Ci(12, 12, 9.4), Ln(5.4, 5.4, 18.6, 18.6)}},
            {"server", {Rc(2.6, 3.6, 18.8, 7, 2), Rc(2.6, 13.4, 18.8, 7, 2), Dt(6.6, 7.1, 0.9), Dt(6.6, 16.9, 0.9)}},
            {"filter", {Pg("3 4.4, 21 4.4, 13.9 12.8, 13.9 20.4, 10.1 18.4, 10.1 12.8")}},
            {"terminal", {Rc(2.6, 3.6, 18.8, 16.8, 2.4), Pl("6.6 9, 9.8 12.2, 6.6 15.4"), Ln(12.4, 15.6, 17.4, 15.6)}},
            {"save", {P("M19 21 H5 a2 2 0 0 1-2-2 V5 a2 2 0 0 1 2-2 h11 l5 5 v11 a2 2 0 0 1-2 2 Z"),
                      Pl("16.8 21, 16.8 13.2, 7.2 13.2, 7.2 21"), Pl("7.2 3, 7.2 8, 15 8")}},
            {"heart", {P("M12 21 L3.6 12.6 a5.4 5.4 0 0 1 7.6-7.6 L12 5.8 l.8-.8 a5.4 5.4 0 0 1 7.6 7.6 Z")}},
            {"list", {Ln(8.4, 6.4, 20.6, 6.4), Ln(8.4, 12, 20.6, 12), Ln(8.4, 17.6, 20.6, 17.6),
                      Dt(4.2, 6.4, 1.1), Dt(4.2, 12, 1.1), Dt(4.2, 17.6, 1.1)}},
            {"cloud", {P("M17.4 19.4 H7 A5.1 5.1 0 0 1 7 9.2 a7 7 0 0 1 13.2 2.2 a4.1 4.1 0 0 1-2.8 8 Z")}},
            {"bug", {P("M8 8.4 a4 4 0 0 1 8 0 v5.6 a4 4 0 0 1-8 0 Z"), Ln(9.6, 5.6, 8, 3.4), Ln(14.4, 5.6, 16, 3.4),
                     Ln(8, 10.4, 4.2, 10.4), Ln(16, 10.4, 19.8, 10.4), Ln(8.4, 15.4, 5, 18.4), Ln(15.6, 15.4, 19, 18.4)}},
            {"star", {Pg("12 2.6, 14.9 8.9, 21.6 9.8, 16.7 14.4, 18 21.2, 12 17.9, 6 21.2, 7.3 14.4, 2.4 9.8, 9.1 8.9")}}
        }

        Public Shared Function Has(name As String) As Boolean
            Return name IsNot Nothing AndAlso Map.ContainsKey(name)
        End Function

        Public Shared Function [Get](name As String) As IconShape()
            Dim s As IconShape() = Nothing
            If name IsNot Nothing AndAlso Map.TryGetValue(name, s) Then Return s
            Return Map("shield")
        End Function

        ''' <summary>Draws <paramref name="name"/> centred inside <paramref name="box"/>.</summary>
        Public Shared Sub Draw(g As Graphics, name As String, box As RectangleF, tint As Color,
                               Optional stroke As Single = 1.9F)
            If g Is Nothing Then Return
            Dim size = Math.Min(box.Width, box.Height)
            If size <= 1 Then Return

            Dim scale = size / 24.0F
            Dim state = g.Save()
            Try
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.TranslateTransform(box.X + (box.Width - size) / 2.0F, box.Y + (box.Height - size) / 2.0F)
                g.ScaleTransform(scale, scale)

                Using pen As New Pen(tint, stroke)
                    pen.StartCap = LineCap.Round
                    pen.EndCap = LineCap.Round
                    pen.LineJoin = LineJoin.Round
                    Using brush As New SolidBrush(tint)
                        For Each sh In [Get](name)
                            RenderShape(g, pen, brush, sh)
                        Next
                    End Using
                End Using
            Finally
                g.Restore(state)
            End Try
        End Sub

        Private Shared Sub RenderShape(g As Graphics, pen As Pen, brush As SolidBrush, sh As IconShape)
            Select Case sh.Kind
                Case ShapeKind.PathData
                    g.DrawPath(pen, SvgPath.Parse(sh.Data))
                Case ShapeKind.Circle
                    g.DrawEllipse(pen, sh.A - sh.C, sh.B - sh.C, sh.C * 2, sh.C * 2)
                Case ShapeKind.Line
                    g.DrawLine(pen, sh.A, sh.B, sh.C, sh.D)
                Case ShapeKind.Rect
                    Using gp = Gfx.RoundedPath(New RectangleF(sh.A, sh.B, sh.C, sh.D), sh.E)
                        g.DrawPath(pen, gp)
                    End Using
                Case ShapeKind.Polyline
                    Dim pts = ParsePoints(sh.Data)
                    If pts.Length > 1 Then g.DrawLines(pen, pts)
                Case ShapeKind.Polygon
                    Dim pts = ParsePoints(sh.Data)
                    If pts.Length > 2 Then g.DrawPolygon(pen, pts)
                Case ShapeKind.Dot
                    g.FillEllipse(brush, sh.A - sh.C, sh.B - sh.C, sh.C * 2, sh.C * 2)
            End Select
        End Sub

        Private Shared ReadOnly PointCache As New Dictionary(Of String, PointF())(StringComparer.Ordinal)

        Private Shared Function ParsePoints(s As String) As PointF()
            SyncLock PointCache
                Dim cached As PointF() = Nothing
                If PointCache.TryGetValue(s, cached) Then Return cached
                Dim raw = s.Split(New Char() {","c, " "c}, StringSplitOptions.RemoveEmptyEntries)
                Dim list As New List(Of PointF)()
                Dim i = 0
                While i + 1 < raw.Length
                    list.Add(New PointF(
                        Single.Parse(raw(i), Globalization.CultureInfo.InvariantCulture),
                        Single.Parse(raw(i + 1), Globalization.CultureInfo.InvariantCulture)))
                    i += 2
                End While
                Dim arr = list.ToArray()
                PointCache(s) = arr
                Return arr
            End SyncLock
        End Function

        ''' <summary>Renders an icon into a bitmap - used for the tray icon and window icon.</summary>
        Public Shared Function ToBitmap(name As String, size As Integer, tint As Color,
                                        Optional background As Color? = Nothing,
                                        Optional stroke As Single = 2.0F) As Bitmap
            Dim bmp As New Bitmap(size, size, Imaging.PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.Clear(Color.Transparent)
                If background.HasValue Then
                    Using b As New SolidBrush(background.Value)
                        g.FillEllipse(b, 0, 0, size - 1, size - 1)
                    End Using
                End If
                Dim pad = size * 0.18F
                Draw(g, name, New RectangleF(pad, pad, size - pad * 2, size - pad * 2), tint, stroke)
            End Using
            Return bmp
        End Function

        ''' <summary>
        ''' Writes a real multi-resolution .ico (16/24/32/48/64/128/256) with PNG-compressed
        ''' frames, so the shipped executable carries a proper Windows icon.
        ''' </summary>
        Public Shared Sub SaveIcoFile(path As String, name As String, tint As Color, background As Color)
            Dim sizes = New Integer() {16, 24, 32, 48, 64, 128, 256}
            Dim frames As New List(Of Byte())()

            For Each s In sizes
                Using bmp = ToBitmap(name, s, tint, background, If(s <= 32, 2.6F, 2.0F))
                    Using ms As New IO.MemoryStream()
                        bmp.Save(ms, Imaging.ImageFormat.Png)
                        frames.Add(ms.ToArray())
                    End Using
                End Using
            Next

            Using fs As New IO.FileStream(path, IO.FileMode.Create, IO.FileAccess.Write)
                Using w As New IO.BinaryWriter(fs)
                    w.Write(CUShort(0))                 ' reserved
                    w.Write(CUShort(1))                 ' type 1 = icon
                    w.Write(CUShort(frames.Count))

                    Dim offset = 6 + 16 * frames.Count
                    For i = 0 To frames.Count - 1
                        Dim s = sizes(i)
                        w.Write(CByte(If(s >= 256, 0, s)))   ' width  (0 means 256)
                        w.Write(CByte(If(s >= 256, 0, s)))   ' height
                        w.Write(CByte(0))                    ' palette size
                        w.Write(CByte(0))                    ' reserved
                        w.Write(CUShort(1))                  ' colour planes
                        w.Write(CUShort(32))                 ' bits per pixel
                        w.Write(CUInt(frames(i).Length))
                        w.Write(CUInt(offset))
                        offset += frames(i).Length
                    Next

                    For Each f In frames
                        w.Write(f)
                    Next
                End Using
            End Using
        End Sub

        Public Shared Function ToIcon(name As String, size As Integer, tint As Color,
                                      Optional background As Color? = Nothing) As Icon
            Using bmp = ToBitmap(name, size, tint, background, 2.2F)
                Dim h = bmp.GetHicon()
                Try
                    Using tmp = Icon.FromHandle(h)
                        Return CType(tmp.Clone(), Icon)
                    End Using
                Finally
                    NativeMethods.DestroyIcon(h)
                End Try
            End Using
        End Function

    End Class

End Namespace
