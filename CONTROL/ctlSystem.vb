Public Class ctlSystem
    Private Sub ctlSystem_Load(sender As Object, e As EventArgs) Handles Me.Load
        ' Retrieve CPU name from the correct registry path
        Dim name As String = My.Computer.Registry.GetValue("HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\SYSTEM\CentralProcessor\0", "ProcessorNameString", Nothing)
        If name IsNot Nothing Then
            lblComputerName.Text = name
        Else
            lblComputerName.Text = "Processor name not found"
        End If

        ' Retrieve CPU speed (in MHz) from the correct registry path
        Dim speed As Object = My.Computer.Registry.GetValue("HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\SYSTEM\CentralProcessor\0", "~MHz", Nothing)
        If speed IsNot Nothing Then
            lblComputerSpeed.Text = speed.ToString() & " MHz"
        Else
            lblComputerSpeed.Text = "Speed not found"
        End If
    End Sub
End Class
