Imports System.Net.Http
Imports System.Reflection
Imports System.Text.Json.Serialization

Namespace Core

    Public Class ReleaseInfo
        Public Property Version As String = ""
        Public Property Published As DateTime = DateTime.MinValue
        Public Property DownloadUrl As String = ""
        Public Property Notes As String = ""
        Public Property Mandatory As Boolean = False
        <JsonPropertyName("signatureVersion")>
        Public Property SignatureVersion As String = ""
        <JsonPropertyName("signatureUrl")>
        Public Property SignatureUrl As String = ""
    End Class

    ''' <summary>
    ''' Checks a vendor-hosted JSON manifest for a newer build. Nothing is downloaded
    ''' or installed automatically - AVAK only tells the user and opens the link they
    ''' choose. The check is opt-in via <see cref="AppSettings.UpdateFeedUrl"/> and is
    ''' the only outbound request the app can make besides a manual signature update.
    ''' </summary>
    Public NotInheritable Class UpdateChecker

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property CurrentVersion As Version
            Get
                Try
                    Return Assembly.GetExecutingAssembly().GetName().Version
                Catch
                    Return New Version(0, 0, 0, 0)
                End Try
            End Get
        End Property

        Public Class CheckResult
            Public Property Checked As Boolean
            Public Property UpdateAvailable As Boolean
            Public Property Message As String = ""
            Public Property Release As ReleaseInfo
        End Class

        Public Shared Async Function CheckAsync(feedUrl As String) As Task(Of CheckResult)
            If String.IsNullOrWhiteSpace(feedUrl) Then
                Return New CheckResult With {.Checked = False, .Message = "No update feed configured."}
            End If

            Try
                Using http As New HttpClient()
                    http.Timeout = TimeSpan.FromSeconds(20)
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("AVAK/" & CurrentVersion.ToString())
                    Dim body = Await http.GetStringAsync(feedUrl)

                    Dim info = Text.Json.JsonSerializer.Deserialize(Of ReleaseInfo)(body, Json.Options)
                    If info Is Nothing OrElse String.IsNullOrWhiteSpace(info.Version) Then
                        Return New CheckResult With {.Checked = True, .Message = "The update feed did not return a version."}
                    End If

                    Dim remote As Version = Nothing
                    If Not Version.TryParse(info.Version, remote) Then
                        Return New CheckResult With {.Checked = True, .Message = "The update feed returned an unreadable version."}
                    End If

                    If remote > CurrentVersion Then
                        Logger.Info($"Update available: {remote} (running {CurrentVersion})")
                        Return New CheckResult With {
                            .Checked = True, .UpdateAvailable = True, .Release = info,
                            .Message = $"AVAK {info.Version} is available (you have {CurrentVersion})."}
                    End If

                    Return New CheckResult With {
                        .Checked = True, .UpdateAvailable = False, .Release = info,
                        .Message = $"You are up to date ({CurrentVersion})."}
                End Using
            Catch ex As Exception
                Logger.Warn("Update check failed: " & ex.Message)
                Return New CheckResult With {.Checked = False, .Message = "Update check failed: " & ex.Message}
            End Try
        End Function

        Public Shared Sub OpenDownload(release As ReleaseInfo)
            If release Is Nothing OrElse String.IsNullOrWhiteSpace(release.DownloadUrl) Then Return
            Try
                Process.Start(New ProcessStartInfo(release.DownloadUrl) With {.UseShellExecute = True})
            Catch ex As Exception
                Logger.Warn("Could not open the download link: " & ex.Message)
            End Try
        End Sub

    End Class

End Namespace
