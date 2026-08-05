Imports System.Globalization

Namespace Core

    ''' <summary>Display formatting helpers (sizes, durations, relative time).</summary>
    Public NotInheritable Class Fmt

        Private Sub New()
        End Sub

        Private Shared ReadOnly Units As String() = {"B", "KB", "MB", "GB", "TB", "PB"}

        Public Shared Function Bytes(n As Long) As String
            If n < 0 Then Return "-"
            Dim v As Double = n
            Dim i As Integer = 0
            While v >= 1024 AndAlso i < Units.Length - 1
                v /= 1024
                i += 1
            End While
            Dim digits = If(i = 0, 0, If(v < 10, 2, If(v < 100, 1, 0)))
            Return v.ToString("N" & digits, CultureInfo.InvariantCulture) & " " & Units(i)
        End Function

        Public Shared Function Duration(ts As TimeSpan) As String
            If ts.TotalSeconds < 1 Then Return CInt(ts.TotalMilliseconds).ToString() & " ms"
            If ts.TotalMinutes < 1 Then Return ts.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) & " s"
            If ts.TotalHours < 1 Then Return ts.Minutes.ToString() & "m " & ts.Seconds.ToString() & "s"
            Return CInt(Math.Floor(ts.TotalHours)).ToString() & "h " & ts.Minutes.ToString() & "m"
        End Function

        Public Shared Function Ago(t As DateTime) As String
            If t = DateTime.MinValue Then Return "never"
            Dim d = DateTime.Now - t
            If d.TotalSeconds < 0 Then Return "just now"
            If d.TotalSeconds < 60 Then Return "just now"
            If d.TotalMinutes < 60 Then Return CInt(d.TotalMinutes).ToString() & " min ago"
            If d.TotalHours < 24 Then Return CInt(d.TotalHours).ToString() & " h ago"
            If d.TotalDays < 7 Then Return CInt(d.TotalDays).ToString() & " d ago"
            Return t.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
        End Function

        Public Shared Function Num(n As Long) As String
            Return n.ToString("N0", CultureInfo.InvariantCulture)
        End Function

        Public Shared Function Pct(v As Double) As String
            Return v.ToString("0.#", CultureInfo.InvariantCulture) & "%"
        End Function

        Public Shared Function Ellipsis(s As String, max As Integer) As String
            If String.IsNullOrEmpty(s) OrElse s.Length <= max Then Return If(s, "")
            If max <= 3 Then Return s.Substring(0, max)
            Return s.Substring(0, max - 3) & "..."
        End Function

        ''' <summary>Shortens a long path to "C:\...\parent\file.ext".</summary>
        Public Shared Function ShortPath(p As String, Optional max As Integer = 62) As String
            If String.IsNullOrEmpty(p) OrElse p.Length <= max Then Return If(p, "")
            Try
                Dim name = IO.Path.GetFileName(p)
                Dim dir = IO.Path.GetDirectoryName(p)
                Dim parent = If(String.IsNullOrEmpty(dir), "", IO.Path.GetFileName(dir))
                Dim rootPart = IO.Path.GetPathRoot(p)
                Dim result = rootPart & "..." & IO.Path.DirectorySeparatorChar & parent & IO.Path.DirectorySeparatorChar & name
                Return If(result.Length <= max, result, "..." & IO.Path.DirectorySeparatorChar & name)
            Catch
                Return Ellipsis(p, max)
            End Try
        End Function

    End Class

End Namespace
