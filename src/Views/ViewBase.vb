Imports System.Drawing
Imports System.Windows.Forms
Imports AVAK.Theme
Imports AVAK.Ui
Imports AVAK.Ui.Controls

Namespace Views

    ''' <summary>Base for every page in the shell.</summary>
    Public MustInherit Class ViewBase
        Inherits Panel

        Protected Const Pad As Integer = 24

        Public MustOverride ReadOnly Property Title As String
        Public MustOverride ReadOnly Property Subtitle As String

        Private _built As Boolean

        Protected Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
            DoubleBuffered = True
            BackColor = ThemeManager.Bg
            Dock = DockStyle.Fill
            AddHandler ThemeManager.Changed, AddressOf ThemeHook
        End Sub

        Private Sub ThemeHook(sender As Object, e As EventArgs)
            If IsDisposed Then Return
            BackColor = ThemeManager.Bg
            Invalidate(True)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then RemoveHandler ThemeManager.Changed, AddressOf ThemeHook
            MyBase.Dispose(disposing)
        End Sub

        ''' <summary>Called the first time the page is shown.</summary>
        Protected MustOverride Sub Build()

        Public Sub EnsureBuilt()
            If _built Then Return
            _built = True
            Build()
            Relayout()
        End Sub

        Public Overridable Sub OnActivated()
        End Sub

        Public Overridable Sub OnDeactivated()
        End Sub

        Public Overridable Sub Relayout()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            If _built Then Relayout()
        End Sub

        ' -- small builders shared by the pages -------------------------------

        Protected Function Card(title As String, icon As String, Optional accent As Color = Nothing) As CatCard
            Dim c As New CatCard With {.Title = title, .IconName = icon}
            If Not accent.IsEmpty Then c.Accent = accent
            Controls.Add(c)
            Return c
        End Function

        Protected Function Lbl(text As String, role As String,
                               Optional colour As Color = Nothing,
                               Optional parent As Control = Nothing) As CatLabel
            Dim l As New CatLabel With {.Text_ = text, .FontRole = role}
            If Not colour.IsEmpty Then l.Tone = colour
            If parent Is Nothing Then Controls.Add(l) Else parent.Controls.Add(l)
            Return l
        End Function

        Protected Function Btn(caption As String, kind As ButtonKind, icon As String,
                               handler As EventHandler, Optional parent As Control = Nothing) As CatButton
            Dim b As New CatButton(caption, kind, icon)
            If handler IsNot Nothing Then AddHandler b.Click, handler
            If parent Is Nothing Then Controls.Add(b) Else parent.Controls.Add(b)
            Return b
        End Function

        Protected Function Table(ParamArray cols As CatColumn()) As CatTable
            Dim t As New CatTable()
            t.SetColumns(cols)
            Controls.Add(t)
            Return t
        End Function

        ''' <summary>
        ''' Confirmation for a delete-style action. Users who turned
        ''' "Confirm before deleting" off are not asked again.
        ''' </summary>
        Protected Function AskDestructive(title As String, message As String, confirmText As String) As Boolean
            Try
                If Not Core.AppSettings.Current.ConfirmBeforeDelete Then Return True
            Catch
            End Try
            Return Ask(title, message, confirmText, True)
        End Function

        Protected Sub Say(message As String, Optional isError As Boolean = False)
            If isError Then Toast.Warn(Title, message) Else Toast.Ok(Title, message)
        End Sub

        Protected Function Ask(title As String, message As String,
                               Optional confirmText As String = "Confirm",
                               Optional destructive As Boolean = False) As Boolean
            Return CatDialog.Confirm(FindForm(), title, message, confirmText,
                                     If(destructive, DialogTone.Danger, DialogTone.Question), destructive)
        End Function

        ''' <summary>Marshals an action onto the UI thread; safe to call from worker threads.</summary>
        Protected Sub Ui_(action As Action)
            If IsDisposed OrElse Not IsHandleCreated Then Return
            Try
                If InvokeRequired Then BeginInvoke(action) Else action()
            Catch
            End Try
        End Sub

    End Class

End Namespace
