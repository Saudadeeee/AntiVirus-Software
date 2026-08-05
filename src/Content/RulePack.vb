Imports System.IO
Imports AVAK.Core
Imports AVAK.Security

Namespace Content

    ''' <summary>
    ''' One rule file. Packs are plain JSON, so anyone can write one in a text editor
    ''' or generate one from a feed. Drop it in the rules folder and it is picked up.
    '''
    ''' Shipped packs live next to the executable under content\rules and are treated
    ''' as read-only; user packs live in %LOCALAPPDATA%\AVAK\rules and are writable.
    ''' </summary>
    Public Class RulePack

        ' -- metadata (all optional except Name) ------------------------------
        Public Property Name As String = ""
        Public Property Version As String = "1"
        Public Property Author As String = ""
        Public Property Description As String = ""
        Public Property License As String = ""
        Public Property Homepage As String = ""
        Public Property Updated As DateTime = DateTime.MinValue

        ' -- content -----------------------------------------------------------
        Public Property Hashes As New List(Of HashSignature)()
        Public Property Patterns As New List(Of PatternSignature)()

        ' -- runtime state, not serialised ------------------------------------
        <Text.Json.Serialization.JsonIgnore>
        Public Property FilePath As String = ""

        <Text.Json.Serialization.JsonIgnore>
        Public Property ReadOnlyPack As Boolean = False

        <Text.Json.Serialization.JsonIgnore>
        Public Property Enabled As Boolean = True

        <Text.Json.Serialization.JsonIgnore>
        Public Property Issues As New List(Of String)()

        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly Property FileName As String
            Get
                Try
                    Return Path.GetFileName(FilePath)
                Catch
                    Return FilePath
                End Try
            End Get
        End Property

        <Text.Json.Serialization.JsonIgnore>
        Public ReadOnly Property RuleCount As Integer
            Get
                Return If(Hashes?.Count, 0) + If(Patterns?.Count, 0)
            End Get
        End Property

        Public Sub Normalise()
            If Hashes Is Nothing Then Hashes = New List(Of HashSignature)()
            If Patterns Is Nothing Then Patterns = New List(Of PatternSignature)()
            If String.IsNullOrWhiteSpace(Name) Then Name = FileName
            For Each p In Patterns
                If p.Extensions Is Nothing Then p.Extensions = New List(Of String)()
            Next
        End Sub

        ''' <summary>
        ''' Checks a pack for problems a human would want to know about. Returns an
        ''' empty list when the pack is clean.
        ''' </summary>
        Public Function Validate() As List(Of String)
            Dim problems As New List(Of String)()
            Normalise()

            If RuleCount = 0 Then problems.Add("The pack contains no rules.")

            Dim seenHash As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For i = 0 To Hashes.Count - 1
                Dim h = Hashes(i)
                Dim where = $"hashes[{i}]"
                If String.IsNullOrWhiteSpace(h.Name) Then problems.Add($"{where}: missing 'name'.")
                If String.IsNullOrWhiteSpace(h.Sha256) AndAlso String.IsNullOrWhiteSpace(h.Md5) Then
                    problems.Add($"{where} ('{h.Name}'): needs 'sha256' or 'md5'.")
                End If
                If Not String.IsNullOrWhiteSpace(h.Sha256) Then
                    If h.Sha256.Trim().Length <> 64 OrElse Not IsHex(h.Sha256.Trim()) Then
                        problems.Add($"{where} ('{h.Name}'): sha256 must be 64 hex characters.")
                    ElseIf Not seenHash.Add(h.Sha256.Trim()) Then
                        problems.Add($"{where} ('{h.Name}'): duplicate sha256 inside this pack.")
                    End If
                End If
                If Not String.IsNullOrWhiteSpace(h.Md5) Then
                    If h.Md5.Trim().Length <> 32 OrElse Not IsHex(h.Md5.Trim()) Then
                        problems.Add($"{where} ('{h.Name}'): md5 must be 32 hex characters.")
                    End If
                End If
            Next

            For i = 0 To Patterns.Count - 1
                Dim p = Patterns(i)
                Dim where = $"patterns[{i}]"
                If String.IsNullOrWhiteSpace(p.Name) Then problems.Add($"{where}: missing 'name'.")

                Dim hasAscii = Not String.IsNullOrEmpty(p.Ascii)
                Dim hasHex = Not String.IsNullOrWhiteSpace(p.Hex)
                If Not hasAscii AndAlso Not hasHex Then
                    problems.Add($"{where} ('{p.Name}'): needs 'ascii' or 'hex'.")
                ElseIf hasAscii AndAlso hasHex Then
                    problems.Add($"{where} ('{p.Name}'): set only one of 'ascii' or 'hex'.")
                End If

                If hasHex Then
                    Dim clean = p.Hex.Replace(" ", "").Replace("-", "")
                    If clean.Length Mod 2 <> 0 Then
                        problems.Add($"{where} ('{p.Name}'): hex must have an even number of characters.")
                    ElseIf Not clean.Replace("?", "0").All(AddressOf IsHexChar) Then
                        problems.Add($"{where} ('{p.Name}'): hex may only contain 0-9, A-F and ?? wildcards.")
                    End If
                End If

                If hasAscii AndAlso p.Ascii.Length < 4 Then
                    problems.Add($"{where} ('{p.Name}'): an ascii pattern shorter than 4 characters will match almost everything.")
                End If

                If p.MaxOffset < 0 Then problems.Add($"{where} ('{p.Name}'): maxOffset cannot be negative.")

                For Each e In p.Extensions
                    If Not e.StartsWith(".") Then
                        problems.Add($"{where} ('{p.Name}'): extension '{e}' should start with a dot.")
                    End If
                Next
            Next

            Return problems
        End Function

        Private Shared Function IsHex(s As String) As Boolean
            Return s.All(AddressOf IsHexChar)
        End Function

        Private Shared Function IsHexChar(c As Char) As Boolean
            Return (c >= "0"c AndAlso c <= "9"c) OrElse
                   (c >= "a"c AndAlso c <= "f"c) OrElse
                   (c >= "A"c AndAlso c <= "F"c)
        End Function

        ''' <summary>A ready-to-edit empty pack.</summary>
        Public Shared Function CreateTemplate(name As String, author As String) As RulePack
            Return New RulePack With {
                .Name = name,
                .Version = "1",
                .Author = author,
                .Description = "Custom rules",
                .License = "",
                .Updated = DateTime.Now,
                .Hashes = New List(Of HashSignature)(),
                .Patterns = New List(Of PatternSignature)()
            }
        End Function

    End Class

End Namespace
