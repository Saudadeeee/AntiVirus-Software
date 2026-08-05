Imports System.Drawing
Imports System.Drawing.Text

Namespace Theme

    ''' <summary>
    ''' Single source of truth for colours + fonts. Controls subscribe to
    ''' <see cref="Changed"/> and repaint; nothing hard-codes an RGB value.
    ''' </summary>
    Public NotInheritable Class ThemeManager

        Private Sub New()
        End Sub

        Private Shared _palette As Palette = Palette.Mocha
        Private Shared _accentName As String = "Mauve"
        Private Shared _fontFamily As String = ResolveFontFamily()
        Private Shared _radius As Integer = 12

        Public Shared Event Changed As EventHandler

        Public Shared ReadOnly Property Colors As Palette
            Get
                Return _palette
            End Get
        End Property

        Public Shared ReadOnly Property Accent As Color
            Get
                Return _palette.Accent(_accentName)
            End Get
        End Property

        Public Shared ReadOnly Property AccentName As String
            Get
                Return _accentName
            End Get
        End Property

        Public Shared ReadOnly Property Radius As Integer
            Get
                Return _radius
            End Get
        End Property

        Public Shared Sub Apply(fl As Flavor, accentName As String, Optional radius As Integer = 12)
            _palette = Palette.Get(fl)
            _accentName = If(String.IsNullOrWhiteSpace(accentName), "Mauve", accentName)
            _radius = Math.Max(0, Math.Min(24, radius))
            RaiseEvent Changed(Nothing, EventArgs.Empty)
        End Sub

        ' -- Semantic shortcuts ----------------------------------------------
        Public Shared ReadOnly Property Bg As Color
            Get
                Return _palette.Base
            End Get
        End Property

        Public Shared ReadOnly Property BgDeep As Color
            Get
                Return _palette.Crust
            End Get
        End Property

        Public Shared ReadOnly Property BgSide As Color
            Get
                Return _palette.Mantle
            End Get
        End Property

        Public Shared ReadOnly Property Card As Color
            Get
                Return _palette.Surface0
            End Get
        End Property

        Public Shared ReadOnly Property CardHover As Color
            Get
                Return _palette.Surface1
            End Get
        End Property

        Public Shared ReadOnly Property Border As Color
            Get
                Return _palette.Surface1
            End Get
        End Property

        Public Shared ReadOnly Property Fg As Color
            Get
                Return _palette.Text
            End Get
        End Property

        Public Shared ReadOnly Property FgMuted As Color
            Get
                Return _palette.Subtext0
            End Get
        End Property

        Public Shared ReadOnly Property FgDim As Color
            Get
                Return _palette.Overlay1
            End Get
        End Property

        Public Shared ReadOnly Property Ok As Color
            Get
                Return _palette.Green
            End Get
        End Property

        Public Shared ReadOnly Property Warn As Color
            Get
                Return _palette.Yellow
            End Get
        End Property

        Public Shared ReadOnly Property Danger As Color
            Get
                Return _palette.Red
            End Get
        End Property

        Public Shared ReadOnly Property Info As Color
            Get
                Return _palette.Sapphire
            End Get
        End Property

        ' -- Typography -------------------------------------------------------
        Private Shared ReadOnly PreferredFonts As String() =
            {"Segoe UI Variable Text", "Segoe UI", "Inter", "Noto Sans", "Tahoma", "Arial"}

        Private Shared Function ResolveFontFamily() As String
            Try
                Using ifc As New InstalledFontCollection()
                    Dim have As New HashSet(Of String)(
                        ifc.Families.Select(Function(f) f.Name), StringComparer.OrdinalIgnoreCase)
                    For Each candidate In PreferredFonts
                        If have.Contains(candidate) Then Return candidate
                    Next
                End Using
            Catch
            End Try
            Return "Segoe UI"
        End Function

        Private Shared ReadOnly FontCache As New Dictionary(Of String, Font)()

        Public Shared Function GetFont(size As Single, Optional style As FontStyle = FontStyle.Regular) As Font
            Dim key = size.ToString("0.##", Globalization.CultureInfo.InvariantCulture) & "|" & CInt(style).ToString()
            SyncLock FontCache
                Dim f As Font = Nothing
                If FontCache.TryGetValue(key, f) Then Return f
                f = New Font(_fontFamily, size, style, GraphicsUnit.Point)
                FontCache(key) = f
                Return f
            End SyncLock
        End Function

        Public Shared ReadOnly Property Display As Font
            Get
                Return GetFont(23.0F, FontStyle.Bold)
            End Get
        End Property

        Public Shared ReadOnly Property Title As Font
            Get
                Return GetFont(15.0F, FontStyle.Bold)
            End Get
        End Property

        Public Shared ReadOnly Property Subtitle As Font
            Get
                Return GetFont(11.5F, FontStyle.Bold)
            End Get
        End Property

        Public Shared ReadOnly Property Body As Font
            Get
                Return GetFont(9.75F)
            End Get
        End Property

        Public Shared ReadOnly Property BodyBold As Font
            Get
                Return GetFont(9.75F, FontStyle.Bold)
            End Get
        End Property

        Public Shared ReadOnly Property Small As Font
            Get
                Return GetFont(8.25F)
            End Get
        End Property

        Public Shared ReadOnly Property Mono As Font
            Get
                Return GetFont(9.0F)
            End Get
        End Property

        ' -- Helpers ----------------------------------------------------------
        Public Shared Function Mix(a As Color, b As Color, t As Single) As Color
            ' NOTE: every channel is widened to Integer first. In VB, Byte - Byte stays
            ' a Byte and throws OverflowException as soon as the result goes negative.
            Dim k = Math.Max(0.0F, Math.Min(1.0F, t))
            Return Color.FromArgb(
                Chan(CInt(a.A), CInt(b.A), k),
                Chan(CInt(a.R), CInt(b.R), k),
                Chan(CInt(a.G), CInt(b.G), k),
                Chan(CInt(a.B), CInt(b.B), k))
        End Function

        Private Shared Function Chan(a As Integer, b As Integer, k As Single) As Integer
            Return Math.Max(0, Math.Min(255, CInt(Math.Round(a + (b - a) * k))))
        End Function

        Public Shared Function Alpha(c As Color, a As Integer) As Color
            Return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B)
        End Function

        ''' <summary>
        ''' Readable foreground for text drawn on top of <paramref name="bg"/>.
        ''' Accent colours in every Catppuccin flavour are light, so filled buttons
        ''' need the near-black crust rather than the light text colour.
        ''' </summary>
        Public Shared Function On_(bg As Color) As Color
            Dim lum = (0.299R * bg.R + 0.587R * bg.G + 0.114R * bg.B) / 255.0R
            Return If(lum > 0.5R, Palette.Mocha.Crust, Palette.Mocha.Text)
        End Function

    End Class

End Namespace
