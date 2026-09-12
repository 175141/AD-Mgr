Public Class ProgressForm
    Public Sub SetProgress(text As String)
        lblProgress.Text = text
        lblProgress.Refresh()
    End Sub
End Class
