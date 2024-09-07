Public Class frmPopup
    Private Sub MyButton2_Click(sender As Object, e As EventArgs) Handles MyButton2.Click
        Application.Exit()
    End Sub

    Private Sub MyButton1_Click(sender As Object, e As EventArgs) Handles MyButton1.Click
        Me.Close()
    End Sub

    Private Sub MyButton1_MouseDown(sender As Object, e As MouseEventArgs) Handles Panel1.MouseDown, Label1.MouseDown, PictureBox8.MouseDown, lblTitle.MouseDown
        If e.Button = Windows.Forms.MouseButtons.Left Then
            MyFunction.ReleaseCapture()

            MyFunction.SendMessage(Handle, MyFunction.WM_NCLBUTTONDOWN, MyFunction.HT_CAPTION, 0)
        End If
    End Sub

End Class