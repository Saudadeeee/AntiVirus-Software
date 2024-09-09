Public Class frmSettings


    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub frmSettings_MouseDown(sender As Object, e As MouseEventArgs) Handles Panel1.MouseDown, lblTitle.MouseDown
        If e.Button = Windows.Forms.MouseButtons.Left Then
            MyFunction.ReleaseCapture()

            MyFunction.SendMessage(Handle, MyFunction.WM_NCLBUTTONDOWN, MyFunction.HT_CAPTION, 0)
        End If
    End Sub
End Class