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
        CtlDashboard2.BringToFront()
        CtlDashboard2.Visible = True
        With btnDashboardN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        imgDashboard.BackColor = MyColor.imageActiveColor

        With btnProtectionN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        imgProtection.BackColor = MyColor.imageDeactiveColor
        With btnPrivacyN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgPrivacy.BackColor = MyColor.imageDeactiveColor
        With btnNotiN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgNoti.BackColor = MyColor.imageDeactiveColor
        With btnAccountN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgAccount.BackColor = MyColor.imageDeactiveColor
        With btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgSetting.BackColor = MyColor.imageDeactiveColor
        With btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub
    Private Sub btnProtectionN_Click(sender As Object, e As EventArgs) Handles btnProtectionN.Click
        CtlScanCenter1.BringToFront()
        CtlScanCenter1.Visible = True
        With btnProtectionN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        imgProtection.BackColor = MyColor.imageActiveColor

        With btnDashboardN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        imgDashboard.BackColor = MyColor.imageDeactiveColor
        With btnPrivacyN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgPrivacy.BackColor = MyColor.imageDeactiveColor
        With btnNotiN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgNoti.BackColor = MyColor.imageDeactiveColor
        With btnAccountN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgAccount.BackColor = MyColor.imageDeactiveColor
        With btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgSetting.BackColor = MyColor.imageDeactiveColor
        With btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub

End Class
