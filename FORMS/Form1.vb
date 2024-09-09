Imports System.Runtime.InteropServices
Public Class Form1
    Private Sub Form1_MouseDown(sender As Object, e As MouseEventArgs) Handles Me.MouseDown, HeaderPanel.MouseDown, lblTitle.MouseDown
        If e.Button = Windows.Forms.MouseButtons.Left Then
            MyFunction.ReleaseCapture()

            MyFunction.SendMessage(Handle, MyFunction.WM_NCLBUTTONDOWN, MyFunction.HT_CAPTION, 0)
        End If
    End Sub
    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        frmPopup.ShowDialog()
    End Sub

    Private Sub btnMinimize_Click(sender As Object, e As EventArgs) Handles btnMinimize.Click
        Me.WindowState = FormWindowState.Minimized
    End Sub

    Private Sub btnDashboardN_Click(sender As Object, e As EventArgs) Handles btnDashboardN.Click
        MyFunction.DashboardClick()
    End Sub
    Private Sub btnProtectionN_Click(sender As Object, e As EventArgs) Handles btnProtectionN.Click
        MyFunction.ProtectionClick()
    End Sub

    Private Sub btnPrivacyN_Click(sender As Object, e As EventArgs) Handles btnPrivacyN.Click
        MyFunction.PrivacyClick()
    End Sub

    Private Sub btnNotiN_Click(sender As Object, e As EventArgs) Handles btnNotiN.Click
        MyFunction.NotiClick()
    End Sub

    Private Sub btnAccountN_Click(sender As Object, e As EventArgs) Handles btnAccountN.Click
        MyFunction.AccountClick()
    End Sub
    Private Sub btnSettingN_Click(sender As Object, e As EventArgs) Handles btnSettingN.Click
        imgSetting.BackColor = MyColor.imageActiveColor
        frmSettings.ShowDialog()

    End Sub

    Private Sub btnHelpN_Click(sender As Object, e As EventArgs) Handles btnHelpN.Click
        Process.Start(MyStrings.Help)
    End Sub
End Class
