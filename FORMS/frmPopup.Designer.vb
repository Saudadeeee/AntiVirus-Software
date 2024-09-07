<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmPopup
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
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
        Me.Panel1 = New System.Windows.Forms.Panel()
        Me.PictureBox8 = New System.Windows.Forms.PictureBox()
        Me.MyButton2 = New NotError.MyButton()
        Me.MyButton1 = New NotError.MyButton()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.Panel1.SuspendLayout()
        CType(Me.PictureBox8, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Panel1
        '
        Me.Panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.Panel1.Controls.Add(Me.lblTitle)
        Me.Panel1.Controls.Add(Me.PictureBox8)
        Me.Panel1.Controls.Add(Me.MyButton2)
        Me.Panel1.Controls.Add(Me.MyButton1)
        Me.Panel1.Controls.Add(Me.Label1)
        Me.Panel1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Panel1.Location = New System.Drawing.Point(0, 0)
        Me.Panel1.Name = "Panel1"
        Me.Panel1.Size = New System.Drawing.Size(380, 233)
        Me.Panel1.TabIndex = 0
        '
        'PictureBox8
        '
        Me.PictureBox8.Image = Global.NotError.My.Resources.Resources.User_ShieldB
        Me.PictureBox8.Location = New System.Drawing.Point(3, 3)
        Me.PictureBox8.Name = "PictureBox8"
        Me.PictureBox8.Size = New System.Drawing.Size(30, 30)
        Me.PictureBox8.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage
        Me.PictureBox8.TabIndex = 11
        Me.PictureBox8.TabStop = False
        '
        'MyButton2
        '
        Me.MyButton2.BottomColor = System.Drawing.Color.FromArgb(CType(CType(166, Byte), Integer), CType(CType(15, Byte), Integer), CType(CType(27, Byte), Integer))
        Me.MyButton2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(163, Byte))
        Me.MyButton2.ForeColor = System.Drawing.SystemColors.ControlLightLight
        Me.MyButton2.Location = New System.Drawing.Point(192, 154)
        Me.MyButton2.Name = "MyButton2"
        Me.MyButton2.Size = New System.Drawing.Size(175, 48)
        Me.MyButton2.TabIndex = 10
        Me.MyButton2.Text = "Yes"
        Me.MyButton2.TopColor = System.Drawing.Color.IndianRed
        Me.MyButton2.UseVisualStyleBackColor = True
        '
        'MyButton1
        '
        Me.MyButton1.BottomColor = System.Drawing.Color.FromArgb(CType(CType(166, Byte), Integer), CType(CType(15, Byte), Integer), CType(CType(27, Byte), Integer))
        Me.MyButton1.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(163, Byte))
        Me.MyButton1.ForeColor = System.Drawing.SystemColors.ControlLightLight
        Me.MyButton1.Location = New System.Drawing.Point(11, 154)
        Me.MyButton1.Name = "MyButton1"
        Me.MyButton1.Size = New System.Drawing.Size(175, 48)
        Me.MyButton1.TabIndex = 9
        Me.MyButton1.Text = "Noooo"
        Me.MyButton1.TopColor = System.Drawing.Color.IndianRed
        Me.MyButton1.UseVisualStyleBackColor = True
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.Font = New System.Drawing.Font("Arial Black", 35.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(163, Byte))
        Me.Label1.ForeColor = System.Drawing.SystemColors.Control
        Me.Label1.Location = New System.Drawing.Point(77, 79)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(232, 53)
        Me.Label1.TabIndex = 8
        Me.Label1.Text = "You sure?"
        '
        'lblTitle
        '
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Font = New System.Drawing.Font("Microsoft Sans Serif", 15.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(163, Byte))
        Me.lblTitle.ForeColor = System.Drawing.SystemColors.ControlLightLight
        Me.lblTitle.Location = New System.Drawing.Point(33, 5)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(257, 25)
        Me.lblTitle.TabIndex = 12
        Me.lblTitle.Text = "AntiVirus By Nankhanhhh"
        '
        'frmPopup
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.FromArgb(CType(CType(115, Byte), Integer), CType(CType(20, Byte), Integer), CType(CType(27, Byte), Integer))
        Me.ClientSize = New System.Drawing.Size(380, 233)
        Me.Controls.Add(Me.Panel1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None
        Me.Name = "frmPopup"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "frmPopup"
        Me.Panel1.ResumeLayout(False)
        Me.Panel1.PerformLayout()
        CType(Me.PictureBox8, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents Panel1 As Panel
    Friend WithEvents Label1 As Label
    Friend WithEvents MyButton2 As MyButton
    Friend WithEvents MyButton1 As MyButton
    Friend WithEvents PictureBox8 As PictureBox
    Friend WithEvents lblTitle As Label
End Class
