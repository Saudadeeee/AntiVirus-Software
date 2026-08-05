Imports System.IO
Imports System.Text

Namespace Core

    Public Enum LogLevel
        Debug_ = 0
        Info = 1
        Warn = 2
        [Error] = 3
    End Enum

    ''' <summary>Thread-safe rolling text log at %LOCALAPPDATA%\AVAK\logs\avak-yyyyMMdd.log.</summary>
    Public NotInheritable Class Logger

        Private Sub New()
        End Sub

        Private Shared ReadOnly Gate As New Object()
        Private Const MaxBytes As Long = 4L * 1024L * 1024L
        Private Shared ReadOnly Utf8 As New UTF8Encoding(False)

        Public Shared Property MinimumLevel As LogLevel = LogLevel.Info

        Public Shared Sub Dbg(msg As String)
            Write(LogLevel.Debug_, msg, Nothing)
        End Sub

        Public Shared Sub Info(msg As String)
            Write(LogLevel.Info, msg, Nothing)
        End Sub

        Public Shared Sub Warn(msg As String)
            Write(LogLevel.Warn, msg, Nothing)
        End Sub

        Public Shared Sub [Error](msg As String, Optional ex As Exception = Nothing)
            Write(LogLevel.Error, msg, ex)
        End Sub

        Private Shared Sub Write(level As LogLevel, msg As String, ex As Exception)
            If level < MinimumLevel Then Return
            Try
                Dim logFile = IO.Path.Combine(AppPaths.Logs, "avak-" & DateTime.Now.ToString("yyyyMMdd") & ".log")
                Dim sb As New StringBuilder()
                sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(" [").
                   Append(Tag(level)).Append("] ").Append(msg)
                If ex IsNot Nothing Then
                    sb.AppendLine().Append("    ").Append(ex.GetType().Name).Append(": ").Append(ex.Message)
                    If ex.StackTrace IsNot Nothing Then sb.AppendLine().Append("    ").Append(ex.StackTrace)
                End If
                sb.AppendLine()

                SyncLock Gate
                    Roll(logFile)
                    IO.File.AppendAllText(logFile, sb.ToString(), Utf8)
                End SyncLock
            Catch
                ' logging must never crash the app
            End Try
        End Sub

        Private Shared Sub Roll(logFile As String)
            Try
                Dim fi As New IO.FileInfo(logFile)
                If fi.Exists AndAlso fi.Length > MaxBytes Then
                    Dim bak = logFile & ".1"
                    If IO.File.Exists(bak) Then IO.File.Delete(bak)
                    IO.File.Move(logFile, bak)
                End If
            Catch
            End Try
        End Sub

        Private Shared Function Tag(l As LogLevel) As String
            Select Case l
                Case LogLevel.Debug_ : Return "DBG"
                Case LogLevel.Warn : Return "WRN"
                Case LogLevel.Error : Return "ERR"
                Case Else : Return "INF"
            End Select
        End Function

        Public Shared Function ReadTail(maxLines As Integer) As String()
            Try
                Dim logFile = IO.Path.Combine(AppPaths.Logs, "avak-" & DateTime.Now.ToString("yyyyMMdd") & ".log")
                If Not IO.File.Exists(logFile) Then Return Array.Empty(Of String)()
                Dim all As String()
                SyncLock Gate
                    Using fs As New IO.FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Using sr As New StreamReader(fs, Utf8)
                            all = sr.ReadToEnd().Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                        End Using
                    End Using
                End SyncLock
                Return all.Skip(Math.Max(0, all.Length - maxLines)).ToArray()
            Catch
                Return Array.Empty(Of String)()
            End Try
        End Function

    End Class

End Namespace
