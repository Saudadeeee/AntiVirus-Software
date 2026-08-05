Imports System.Drawing

Namespace Theme

    ''' <summary>Catppuccin flavors. Mocha is the AVAK default.</summary>
    Public Enum Flavor
        Mocha = 0
        Macchiato = 1
        Frappe = 2
        Latte = 3
    End Enum

    ''' <summary>
    ''' A full Catppuccin colour set. Field names follow the official spec so the
    ''' values can be checked against https://catppuccin.com/palette 1:1.
    ''' </summary>
    Public NotInheritable Class Palette

        Public ReadOnly Property Flavor As Flavor
        Public ReadOnly Property Name As String
        Public ReadOnly Property IsDark As Boolean

        Public ReadOnly Property Rosewater As Color
        Public ReadOnly Property Flamingo As Color
        Public ReadOnly Property Pink As Color
        Public ReadOnly Property Mauve As Color
        Public ReadOnly Property Red As Color
        Public ReadOnly Property Maroon As Color
        Public ReadOnly Property Peach As Color
        Public ReadOnly Property Yellow As Color
        Public ReadOnly Property Green As Color
        Public ReadOnly Property Teal As Color
        Public ReadOnly Property Sky As Color
        Public ReadOnly Property Sapphire As Color
        Public ReadOnly Property Blue As Color
        Public ReadOnly Property Lavender As Color

        Public ReadOnly Property Text As Color
        Public ReadOnly Property Subtext1 As Color
        Public ReadOnly Property Subtext0 As Color
        Public ReadOnly Property Overlay2 As Color
        Public ReadOnly Property Overlay1 As Color
        Public ReadOnly Property Overlay0 As Color
        Public ReadOnly Property Surface2 As Color
        Public ReadOnly Property Surface1 As Color
        Public ReadOnly Property Surface0 As Color
        Public ReadOnly Property Base As Color
        Public ReadOnly Property Mantle As Color
        Public ReadOnly Property Crust As Color

        Private Sub New(fl As Flavor, nm As String, dark As Boolean, hex As String())
            _Flavor = fl
            _Name = nm
            _IsDark = dark
            _Rosewater = Hx(hex(0)) : _Flamingo = Hx(hex(1)) : _Pink = Hx(hex(2)) : _Mauve = Hx(hex(3))
            _Red = Hx(hex(4)) : _Maroon = Hx(hex(5)) : _Peach = Hx(hex(6)) : _Yellow = Hx(hex(7))
            _Green = Hx(hex(8)) : _Teal = Hx(hex(9)) : _Sky = Hx(hex(10)) : _Sapphire = Hx(hex(11))
            _Blue = Hx(hex(12)) : _Lavender = Hx(hex(13))
            _Text = Hx(hex(14)) : _Subtext1 = Hx(hex(15)) : _Subtext0 = Hx(hex(16))
            _Overlay2 = Hx(hex(17)) : _Overlay1 = Hx(hex(18)) : _Overlay0 = Hx(hex(19))
            _Surface2 = Hx(hex(20)) : _Surface1 = Hx(hex(21)) : _Surface0 = Hx(hex(22))
            _Base = Hx(hex(23)) : _Mantle = Hx(hex(24)) : _Crust = Hx(hex(25))
        End Sub

        Private Shared Function Hx(s As String) As Color
            Return Color.FromArgb(255,
                                  Convert.ToInt32(s.Substring(0, 2), 16),
                                  Convert.ToInt32(s.Substring(2, 2), 16),
                                  Convert.ToInt32(s.Substring(4, 2), 16))
        End Function

        ' -- Official palettes ------------------------------------------------
        ' order: rosewater flamingo pink mauve red maroon peach yellow green teal
        '        sky sapphire blue lavender text subtext1 subtext0 overlay2 overlay1
        '        overlay0 surface2 surface1 surface0 base mantle crust

        Public Shared ReadOnly Mocha As New Palette(Theme.Flavor.Mocha, "Mocha", True, New String() {
            "f5e0dc", "f2cdcd", "f5c2e7", "cba6f7", "f38ba8", "eba0ac", "fab387", "f9e2af", "a6e3a1", "94e2d5",
            "89dceb", "74c7ec", "89b4fa", "b4befe", "cdd6f4", "bac2de", "a6adc8", "9399b2", "7f849c",
            "6c7086", "585b70", "45475a", "313244", "1e1e2e", "181825", "11111b"})

        Public Shared ReadOnly Macchiato As New Palette(Theme.Flavor.Macchiato, "Macchiato", True, New String() {
            "f4dbd6", "f0c6c6", "f5bde6", "c6a0f6", "ed8796", "ee99a0", "f5a97f", "eed49f", "a6da95", "8bd5ca",
            "91d7e3", "7dc4e4", "8aadf4", "b7bdf8", "cad3f5", "b8c0e0", "a5adcb", "939ab7", "8087a2",
            "6e738d", "5b6078", "494d64", "363a4f", "24273a", "1e2030", "181926"})

        Public Shared ReadOnly Frappe As New Palette(Theme.Flavor.Frappe, "Frappe", True, New String() {
            "f2d5cf", "eebebe", "f4b8e4", "ca9ee6", "e78284", "ea999c", "ef9f76", "e5c890", "a6d189", "81c8be",
            "99d1db", "85c1dc", "8caaee", "babbf1", "c6d0f5", "b5bfe2", "a5adce", "949cbb", "838ba7",
            "737994", "626880", "51576d", "414559", "303446", "292c3c", "232634"})

        Public Shared ReadOnly Latte As New Palette(Theme.Flavor.Latte, "Latte", False, New String() {
            "dc8a78", "dd7878", "ea76cb", "8839ef", "d20f39", "e64553", "fe640b", "df8e1d", "40a02b", "179299",
            "04a5e5", "209fb5", "1e66f5", "7287fd", "4c4f69", "5c5f77", "6c6f85", "7c7f93", "8c8fa1",
            "9ca0b0", "acb0be", "bcc0cc", "ccd0da", "eff1f5", "e6e9ef", "dce0e8"})

        Public Shared ReadOnly All As Palette() = New Palette() {Mocha, Macchiato, Frappe, Latte}

        Public Shared Function [Get](fl As Flavor) As Palette
            For Each p In All
                If p.Flavor = fl Then Return p
            Next
            Return Mocha
        End Function

        ''' <summary>Named accent lookup so settings can persist "Mauve" instead of an ARGB int.</summary>
        Public Function Accent(name As String) As Color
            Select Case If(name, "").Trim().ToLowerInvariant()
                Case "rosewater" : Return Rosewater
                Case "flamingo" : Return Flamingo
                Case "pink" : Return Pink
                Case "mauve" : Return Mauve
                Case "red" : Return Red
                Case "maroon" : Return Maroon
                Case "peach" : Return Peach
                Case "yellow" : Return Yellow
                Case "green" : Return Green
                Case "teal" : Return Teal
                Case "sky" : Return Sky
                Case "sapphire" : Return Sapphire
                Case "blue" : Return Blue
                Case "lavender" : Return Lavender
                Case Else : Return Mauve
            End Select
        End Function

        Public Shared ReadOnly AccentNames As String() = New String() {
            "Rosewater", "Flamingo", "Pink", "Mauve", "Red", "Maroon", "Peach",
            "Yellow", "Green", "Teal", "Sky", "Sapphire", "Blue", "Lavender"}

    End Class

End Namespace
