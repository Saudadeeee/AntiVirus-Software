Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Namespace Ui

    Friend NotInheritable Class NativeMethods
        Private Sub New()
        End Sub

        Public Const WM_NCLBUTTONDOWN As Integer = &HA1
        Public Const HTCAPTION As Integer = &H2

        <DllImport("user32.dll", CharSet:=CharSet.Auto)>
        Public Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll")>
        Public Shared Function ReleaseCapture() As Boolean
        End Function

        <DllImport("user32.dll", SetLastError:=True)>
        Public Shared Function DestroyIcon(handle As IntPtr) As Boolean
        End Function

        ''' <summary>Lets a borderless window be dragged by any child surface.</summary>
        Public Shared Sub DragWindow(f As Form)
            If f Is Nothing OrElse f.IsDisposed Then Return
            ReleaseCapture()
            SendMessage(f.Handle, WM_NCLBUTTONDOWN, New IntPtr(HTCAPTION), IntPtr.Zero)
        End Sub
    End Class

    ''' <summary>GDI+ drawing helpers shared by every custom control.</summary>
    Public NotInheritable Class Gfx

        Private Sub New()
        End Sub

        Public Shared Function RoundedPath(r As RectangleF, radius As Single) As GraphicsPath
            Dim gp As New GraphicsPath()
            Dim d = Math.Max(0.0F, Math.Min(radius, Math.Min(r.Width, r.Height) / 2.0F)) * 2.0F
            If d <= 0.01F Then
                gp.AddRectangle(r)
                gp.CloseFigure()
                Return gp
            End If
            gp.AddArc(r.X, r.Y, d, d, 180, 90)
            gp.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            gp.CloseFigure()
            Return gp
        End Function

        Public Shared Sub FillRounded(g As Graphics, r As RectangleF, radius As Single, c As Color)
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            Using gp = RoundedPath(r, radius), b As New SolidBrush(c)
                g.FillPath(b, gp)
            End Using
        End Sub

        Public Shared Sub DrawRounded(g As Graphics, r As RectangleF, radius As Single, c As Color,
                                      Optional width As Single = 1.0F)
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            Dim inset = width / 2.0F
            Dim rr = New RectangleF(r.X + inset, r.Y + inset, r.Width - width, r.Height - width)
            Using gp = RoundedPath(rr, Math.Max(0, radius - inset)), p As New Pen(c, width)
                g.DrawPath(p, gp)
            End Using
        End Sub

        Public Shared Sub GradientRounded(g As Graphics, r As RectangleF, radius As Single,
                                          c1 As Color, c2 As Color, Optional angle As Single = 90.0F)
            If r.Width <= 0 OrElse r.Height <= 0 Then Return
            Using gp = RoundedPath(r, radius)
                Using br As New LinearGradientBrush(New RectangleF(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)),
                                                    c1, c2, angle)
                    g.FillPath(br, gp)
                End Using
            End Using
        End Sub

        ''' <summary>Cheap soft shadow: a few translucent rounded rects fanned outwards.</summary>
        Public Shared Sub DropShadow(g As Graphics, r As RectangleF, radius As Single,
                                     Optional spread As Integer = 6, Optional strength As Integer = 12)
            For i = spread To 1 Step -1
                Dim a = CInt(strength * (1.0F - (i - 1) / CSng(spread)) / spread * 2)
                If a <= 0 Then Continue For
                Dim rr = RectangleF.Inflate(r, i, i)
                rr.Offset(0, i * 0.35F)
                FillRounded(g, rr, radius + i, Color.FromArgb(Math.Min(60, a), 0, 0, 0))
            Next
        End Sub

        Public Shared Sub TextIn(g As Graphics, s As String, f As Font, c As Color, r As RectangleF,
                                 Optional hAlign As StringAlignment = StringAlignment.Near,
                                 Optional vAlign As StringAlignment = StringAlignment.Center,
                                 Optional trim As StringTrimming = StringTrimming.EllipsisCharacter,
                                 Optional wrap As Boolean = False)
            If String.IsNullOrEmpty(s) Then Return
            Using br As New SolidBrush(c), sf As New StringFormat(StringFormat.GenericTypographic)
                sf.Alignment = hAlign
                sf.LineAlignment = vAlign
                sf.Trimming = trim
                sf.FormatFlags = If(wrap, CType(0, StringFormatFlags), StringFormatFlags.NoWrap)
                g.TextRenderingHint = Text.TextRenderingHint.ClearTypeGridFit
                g.DrawString(s, f, br, r, sf)
            End Using
        End Sub

        Public Shared Function Measure(g As Graphics, s As String, f As Font) As SizeF
            If String.IsNullOrEmpty(s) Then Return SizeF.Empty
            Using sf As New StringFormat(StringFormat.GenericTypographic)
                sf.FormatFlags = StringFormatFlags.NoWrap
                Return g.MeasureString(s, f, Integer.MaxValue, sf)
            End Using
        End Function

        Public Shared Sub Quality(g As Graphics)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.PixelOffsetMode = PixelOffsetMode.HighQuality
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.CompositingQuality = CompositingQuality.HighQuality
        End Sub

        ''' <summary>Ease-out cubic, used by every micro-animation in the app.</summary>
        Public Shared Function EaseOut(t As Single) As Single
            Dim k = 1.0F - Math.Max(0.0F, Math.Min(1.0F, t))
            Return 1.0F - k * k * k
        End Function

    End Class

End Namespace
