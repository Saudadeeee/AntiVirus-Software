Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports AVAK.Content
Imports AVAK.Core

Namespace Security

    Public Class HashSignature
        Public Property Sha256 As String = ""
        Public Property Md5 As String = ""
        Public Property Name As String = ""
        Public Property Severity As Severity = Severity.High
    End Class

    Public Class PatternSignature
        Public Property Name As String = ""
        ''' <summary>Hex byte pattern, "??" means wildcard. e.g. "4D5A??00".</summary>
        Public Property Hex As String = ""
        ''' <summary>Plain ASCII alternative to <see cref="Hex"/>.</summary>
        Public Property Ascii As String = ""
        Public Property Severity As Severity = Severity.High
        ''' <summary>Only look at the first N bytes (0 = whole scanned window).</summary>
        Public Property MaxOffset As Integer = 0
        Public Property Extensions As New List(Of String)()
    End Class

    ''' <summary>
    ''' Matching primitives and file hashing.
    '''
    ''' Rules themselves live in <see cref="RuleStore"/>; this class compiles a
    ''' pattern into a searchable form, searches a buffer, and hashes files. It also
    ''' keeps a small facade over the store so existing callers keep working.
    ''' </summary>
    Public NotInheritable Class SignatureDatabase

        Private Sub New()
        End Sub

        ''' <summary>A pattern turned into bytes plus a "must match" mask.</summary>
        Public Class CompiledPattern
            Public Property Name As String
            Public Property Severity As Severity
            Public Property MaxOffset As Integer
            Public Property Extensions As HashSet(Of String)
            Public Bytes As Byte()
            Public Mask As Boolean()     ' True = must match
        End Class

        ' -- facade over the rule store ---------------------------------------

        Public Shared Sub EnsureLoaded()
            RuleStore.EnsureLoaded()
        End Sub

        Public Shared Sub Reload()
            RuleStore.Reload()
        End Sub

        Public Shared ReadOnly Property Version As String
            Get
                Return RuleStore.Version
            End Get
        End Property

        Public Shared ReadOnly Property HashCount As Integer
            Get
                Return RuleStore.HashCount
            End Get
        End Property

        Public Shared ReadOnly Property PatternCount As Integer
            Get
                Return RuleStore.PatternCount
            End Get
        End Property

        Public Shared Function MatchHash(sha256 As String, md5 As String) As HashSignature
            Dim m = RuleStore.MatchHash(sha256, md5)
            Return If(m Is Nothing, Nothing, m.Signature)
        End Function

        Public Shared Function MatchPatterns(buffer As Byte(), length As Integer, extension As String) As List(Of CompiledPattern)
            Return RuleStore.MatchPatterns(buffer, length, extension).Select(Function(m) m.Pattern).ToList()
        End Function

        ' -- compilation and search -------------------------------------------

        ''' <summary>Turns a rule-file pattern into a compiled one. Returns Nothing when malformed.</summary>
        Public Shared Function Compile(p As PatternSignature) As CompiledPattern
            Try
                Dim bytes As New List(Of Byte)()
                Dim mask As New List(Of Boolean)()

                If Not String.IsNullOrEmpty(p.Ascii) Then
                    For Each b In Encoding.ASCII.GetBytes(p.Ascii)
                        bytes.Add(b)
                        mask.Add(True)
                    Next
                ElseIf Not String.IsNullOrWhiteSpace(p.Hex) Then
                    Dim clean = p.Hex.Replace(" ", "").Replace("-", "")
                    If clean.Length Mod 2 <> 0 Then Return Nothing
                    For i = 0 To clean.Length - 2 Step 2
                        Dim pair = clean.Substring(i, 2)
                        If pair = "??" Then
                            bytes.Add(0)
                            mask.Add(False)
                        Else
                            bytes.Add(Convert.ToByte(pair, 16))
                            mask.Add(True)
                        End If
                    Next
                Else
                    Return Nothing
                End If

                If bytes.Count = 0 Then Return Nothing

                Return New CompiledPattern With {
                    .Name = p.Name,
                    .Severity = p.Severity,
                    .MaxOffset = p.MaxOffset,
                    .Bytes = bytes.ToArray(),
                    .Mask = mask.ToArray(),
                    .Extensions = New HashSet(Of String)(
                        If(p.Extensions, New List(Of String)()).Select(Function(s) s.ToLowerInvariant()),
                        StringComparer.OrdinalIgnoreCase)
                }
            Catch ex As Exception
                Logger.Warn("Bad pattern '" & If(p.Name, "?") & "': " & ex.Message)
                Return Nothing
            End Try
        End Function

        ''' <summary>First offset where the pattern matches, or -1.</summary>
        Public Shared Function IndexOfPattern(buf As Byte(), limit As Integer, p As CompiledPattern) As Integer
            Dim n = p.Bytes.Length
            If n = 0 OrElse limit < n Then Return -1
            Dim last = limit - n
            Dim first = p.Bytes(0)
            Dim firstFixed = p.Mask(0)

            For i = 0 To last
                If firstFixed AndAlso buf(i) <> first Then Continue For
                Dim ok = True
                For j = 1 To n - 1
                    If p.Mask(j) AndAlso buf(i + j) <> p.Bytes(j) Then
                        ok = False
                        Exit For
                    End If
                Next
                If ok Then Return i
            Next
            Return -1
        End Function

        ' -- hashing -----------------------------------------------------------

        Public Shared Function Sha256File(path As String) As String
            Try
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536)
                    Using sha = SHA256.Create()
                        Return Hex(sha.ComputeHash(fs))
                    End Using
                End Using
            Catch
                Return ""
            End Try
        End Function

        Public Shared Function Md5File(path As String) As String
            Try
                Using fs As New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536)
                    Using hasher = MD5.Create()
                        Return Hex(hasher.ComputeHash(fs))
                    End Using
                End Using
            Catch
                Return ""
            End Try
        End Function

        Public Shared Function Sha256Bytes(data As Byte(), length As Integer) As String
            Using sha = SHA256.Create()
                Return Hex(sha.ComputeHash(data, 0, length))
            End Using
        End Function

        Public Shared Function Hex(b As Byte()) As String
            Dim sb As New StringBuilder(b.Length * 2)
            For Each x In b
                sb.Append(x.ToString("x2"))
            Next
            Return sb.ToString()
        End Function

        ' -- remote updates ----------------------------------------------------

        ''' <summary>Downloads a rule pack from a URL the user configured and imports it.</summary>
        Public Shared Async Function UpdateFromUrlAsync(url As String) As Task(Of String)
            If String.IsNullOrWhiteSpace(url) Then Return "No update URL configured."
            Try
                Using http As New HttpClient()
                    http.Timeout = TimeSpan.FromSeconds(30)
                    Dim body = Await http.GetStringAsync(url)

                    Dim tmp = Path.Combine(AppPaths.Data, "download.rules.json")
                    File.WriteAllText(tmp, body, New UTF8Encoding(False))

                    Dim result = RuleStore.ImportPack(tmp)
                    Try
                        File.Delete(tmp)
                    Catch
                    End Try
                    Return result.Message
                End Using
            Catch ex As Exception
                Return "Update failed: " & ex.Message
            End Try
        End Function

        ''' <summary>Adds a hash to the user's default pack (used by "mark as malicious").</summary>
        Public Shared Function AddUserHash(sha256 As String, name As String, sev As Severity) As Boolean
            Return RuleStore.AddHash(sha256, name, sev).Ok
        End Function

        Public Shared Function ImportFrom(path As String) As String
            Return RuleStore.ImportPack(path).Message
        End Function

    End Class

End Namespace
