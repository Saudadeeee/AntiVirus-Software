Imports System.Runtime.InteropServices

Public Class MyFunction
    Public Const WM_NCLBUTTONDOWN As Integer = &HA1
    Public Const HT_CAPTION As Integer = &H2

    <DllImportAttribute("user32.dll")>
    Public Shared Function SendMessage(ByVal hWnd As IntPtr, ByVal Msg As Integer, ByVal wParam As Integer, ByVal lParam As Integer) As Integer
    End Function

    <DllImportAttribute("user32.dll")>
    Public Shared Function ReleaseCapture() As Boolean
    End Function

    Public Shared Sub DashboardClick()
        Form1.CtlDashboard2.BringToFront()
        Form1.CtlDashboard2.Visible = True
        With Form1.btnDashboardN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        Form1.imgDashboard.BackColor = MyColor.imageActiveColor

        With Form1.btnProtectionN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        Form1.imgProtection.BackColor = MyColor.imageDeactiveColor
        With Form1.btnPrivacyN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgPrivacy.BackColor = MyColor.imageDeactiveColor
        With Form1.btnNotiN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgNoti.BackColor = MyColor.imageDeactiveColor
        With Form1.btnAccountN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgAccount.BackColor = MyColor.imageDeactiveColor
        With Form1.btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgSetting.BackColor = MyColor.imageDeactiveColor
        With Form1.btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub

    Public Shared Sub ProtectionClick()
        Form1.CtlScanCenter1.BringToFront()
        Form1.CtlScanCenter1.Visible = True
        With Form1.btnProtectionN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        Form1.imgProtection.BackColor = MyColor.imageActiveColor

        With Form1.btnDashboardN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        Form1.imgDashboard.BackColor = MyColor.imageDeactiveColor
        With Form1.btnPrivacyN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgPrivacy.BackColor = MyColor.imageDeactiveColor
        With Form1.btnNotiN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgNoti.BackColor = MyColor.imageDeactiveColor
        With Form1.btnAccountN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgAccount.BackColor = MyColor.imageDeactiveColor
        With Form1.btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgSetting.BackColor = MyColor.imageDeactiveColor
        With Form1.btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub

    Public Shared Sub PrivacyClick()
        Form1.CtlPrivacy1.BringToFront()
        Form1.CtlPrivacy1.Visible = True
        With Form1.btnPrivacyN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        Form1.imgPrivacy.BackColor = MyColor.imageActiveColor

        With Form1.btnDashboardN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        Form1.imgDashboard.BackColor = MyColor.imageDeactiveColor
        With Form1.btnProtectionN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgProtection.BackColor = MyColor.imageDeactiveColor
        With Form1.btnNotiN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgNoti.BackColor = MyColor.imageDeactiveColor
        With Form1.btnAccountN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgAccount.BackColor = MyColor.imageDeactiveColor
        With Form1.btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgSetting.BackColor = MyColor.imageDeactiveColor
        With Form1.btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub

    Public Shared Sub NotiClick()
        Form1.CtlSystem1.BringToFront()
        Form1.CtlSystem1.Visible = True
        With Form1.btnNotiN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        Form1.imgNoti.BackColor = MyColor.imageActiveColor

        With Form1.btnDashboardN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        Form1.imgDashboard.BackColor = MyColor.imageDeactiveColor
        With Form1.btnProtectionN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgProtection.BackColor = MyColor.imageDeactiveColor
        With Form1.btnPrivacyN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgPrivacy.BackColor = MyColor.imageDeactiveColor
        With Form1.btnAccountN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgAccount.BackColor = MyColor.imageDeactiveColor
        With Form1.btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgSetting.BackColor = MyColor.imageDeactiveColor
        With Form1.btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub

    Public Shared Sub AccountClick()

        Form1.CtlAccount1.BringToFront()
        Form1.CtlAccount1.Visible = True
        With Form1.btnAccountN
            .BottomColor = MyColor.active_color1
            .TopColor = MyColor.active_color2
        End With

        Form1.imgAccount.BackColor = MyColor.imageActiveColor

        With Form1.btnDashboardN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With

        Form1.imgDashboard.BackColor = MyColor.imageDeactiveColor
        With Form1.btnProtectionN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgProtection.BackColor = MyColor.imageDeactiveColor
        With Form1.btnPrivacyN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgPrivacy.BackColor = MyColor.imageDeactiveColor
        With Form1.btnNotiN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgNoti.BackColor = MyColor.imageDeactiveColor
        With Form1.btnSettingN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgSetting.BackColor = MyColor.imageDeactiveColor
        With Form1.btnHelpN
            .BottomColor = MyColor.deactive_color1
            .TopColor = MyColor.deactive_color2
        End With
        Form1.imgHelp.BackColor = MyColor.imageDeactiveColor
    End Sub

End Class
