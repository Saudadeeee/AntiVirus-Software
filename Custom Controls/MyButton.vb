Imports System.Drawing.Drawing2D
Imports System.Windows.Forms



Public Class MyButton
    Inherits Button

    Private m_TopColor As Color = Color.IndianRed
    Private m_BottomColor As Color = Color.FromArgb(140, 3, 3)

    Public Property TopColor As Color
        Get
            Return m_TopColor
        End Get
        Set(value As Color)
            m_TopColor = value
            Me.Invalidate()

        End Set
    End Property

    Public Property BottomColor As Color
        Get
            Return m_BottomColor
        End Get
        Set(value As Color)
            m_BottomColor = value
            Me.Invalidate()

        End Set

    End Property

    Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)


        MyBase.OnPaint(e)

        Using lgb As LinearGradientBrush = New LinearGradientBrush(Me.ClientRectangle, m_TopColor, m_BottomColor, 90.0F)
            Using textBrush As SolidBrush = New SolidBrush(Me.ForeColor)
                Using Format As StringFormat = New StringFormat()
                    Format.Alignment = GetHorizontalAlignment()
                    Format.LineAlignment = GetVerticalAlignment()
                    e.Graphics.FillRectangle(lgb, Me.ClientRectangle)
                    e.Graphics.DrawString(Me.Text, Me.Font, textBrush, Me.ClientRectangle, Format)

                End Using
            End Using
        End Using
    End Sub

    Private Function GetHorizontalAlignment() As StringAlignment
        Return CType(Math.Log(Me.TextAlign, 2D) Mod 4, StringAlignment)
    End Function

    Private Function GetVerticalAlignment() As StringAlignment
        Return CType(Math.Log(Me.TextAlign, 2D) / 4, StringAlignment)
    End Function
End Class
