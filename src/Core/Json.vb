Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Core

    ''' <summary>Small wrapper so every persisted file uses the same JSON options and
    ''' a corrupt file never takes the app down.</summary>
    Public NotInheritable Class Json

        Private Sub New()
        End Sub

        Public Shared ReadOnly Options As JsonSerializerOptions = Build()

        ''' <summary>UTF-8 without a BOM so other tools can read the files unaided.</summary>
        Private Shared ReadOnly Utf8 As New UTF8Encoding(False)

        Private Shared Function Build() As JsonSerializerOptions
            Dim o As New JsonSerializerOptions() With {
                .WriteIndented = True,
                .PropertyNameCaseInsensitive = True,
                .PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                .ReadCommentHandling = JsonCommentHandling.Skip,
                .AllowTrailingCommas = True
            }
            o.Converters.Add(New JsonStringEnumConverter())
            Return o
        End Function

        Public Shared Function Load(Of T As Class)(filePath As String) As T
            Try
                If Not IO.File.Exists(filePath) Then Return Nothing
                Dim text = IO.File.ReadAllText(filePath, Encoding.UTF8)
                If String.IsNullOrWhiteSpace(text) Then Return Nothing
                Return JsonSerializer.Deserialize(Of T)(text, Options)
            Catch ex As Exception
                Logger.Warn("Could not read " & filePath & ": " & ex.Message)
                Try
                    ' keep the broken file for post-mortem instead of silently losing it
                    If IO.File.Exists(filePath) Then IO.File.Copy(filePath, filePath & ".corrupt", True)
                Catch
                End Try
                Return Nothing
            End Try
        End Function

        Public Shared Function Save(Of T)(filePath As String, value As T) As Boolean
            Try
                Dim dir = IO.Path.GetDirectoryName(filePath)
                If Not String.IsNullOrEmpty(dir) AndAlso Not IO.Directory.Exists(dir) Then IO.Directory.CreateDirectory(dir)
                Dim tmp = filePath & ".tmp"
                IO.File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options), Utf8)
                ' atomic-ish replace so a crash mid-write cannot truncate the real file
                If IO.File.Exists(filePath) Then
                    IO.File.Replace(tmp, filePath, Nothing)
                Else
                    IO.File.Move(tmp, filePath)
                End If
                Return True
            Catch ex As Exception
                Logger.Error("Could not write " & filePath, ex)
                Return False
            End Try
        End Function

    End Class

End Namespace
