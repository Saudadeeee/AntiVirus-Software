<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ctlAccount
    Inherits System.Windows.Forms.UserControl

    'UserControl overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.imgAccount = New System.Windows.Forms.PictureBox()
        CType(Me.imgAccount, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Arial Black", 35.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(163, Byte))
        Me.Label1.ForeColor = System.Drawing.SystemColors.Control
        Me.Label1.Location = New System.Drawing.Point(17, 21)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(303, 53)
        Me.Label1.TabIndex = 9
        Me.Label1.Text = "User Account"
        '
        'imgAccount
        '
        Me.imgAccount.Image = Global.NotError.My.Resources.Resources.User_Shield
        Me.imgAccount.Location = New System.Drawing.Point(272, 150)
        Me.imgAccount.Name = "imgAccount"
        Me.imgAccount.Size = New System.Drawing.Size(226, 218)
        Me.imgAccount.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        Me.imgAccount.TabIndex = 10
        Me.imgAccount.TabStop = False
        '
        'ctlAccount
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(115, Byte), Integer), CType(CType(20, Byte), Integer), CType(CType(27, Byte), Integer))
        Me.Controls.Add(Me.imgAccount)
        Me.Controls.Add(Me.Label1)
        Me.Name = "ctlAccount"
        Me.Size = New System.Drawing.Size(922, 575)
        CType(Me.imgAccount, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents Label1 As Label
    Friend WithEvents imgAccount As PictureBox
End Class
