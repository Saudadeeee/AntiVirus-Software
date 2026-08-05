Imports System.Media

Namespace Core

    ''' <summary>
    ''' Optional audio feedback. Uses the Windows system sound scheme so AVAK never
    ''' ships audio assets and always respects the user's sound theme (including
    ''' "No Sounds", where these calls become silent no-ops).
    ''' </summary>
    Public NotInheritable Class Sfx

        Private Sub New()
        End Sub

        Private Shared _lastPlayed As DateTime = DateTime.MinValue

        Private Shared Function Allowed() As Boolean
            Try
                If Not AppSettings.Current.PlaySounds Then Return False
            Catch
                Return False
            End Try
            ' a burst of realtime detections must not turn into a burst of beeps
            If (DateTime.UtcNow - _lastPlayed).TotalMilliseconds < 1200 Then Return False
            _lastPlayed = DateTime.UtcNow
            Return True
        End Function

        Public Shared Sub Success()
            If Allowed() Then Safe(Sub() SystemSounds.Asterisk.Play())
        End Sub

        Public Shared Sub Warning()
            If Allowed() Then Safe(Sub() SystemSounds.Exclamation.Play())
        End Sub

        Public Shared Sub Threat()
            If Allowed() Then Safe(Sub() SystemSounds.Hand.Play())
        End Sub

        Private Shared Sub Safe(play As Action)
            Try
                play()
            Catch
                ' no audio device, or the sound scheme is silent
            End Try
        End Sub

    End Class

End Namespace
