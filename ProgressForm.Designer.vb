<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ProgressForm
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.lblProgress = New System.Windows.Forms.Label()
        Me.SuspendLayout()
        '
        'lblProgress
        '
        Me.lblProgress.AutoSize = False
        Me.lblProgress.Location = New System.Drawing.Point(20, 20)
        Me.lblProgress.Name = "lblProgress"
        Me.lblProgress.Size = New System.Drawing.Size(260, 40)
        Me.lblProgress.TabIndex = 0
        Me.lblProgress.Text = "正在处理..."
        Me.lblProgress.TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        '
        'ProgressForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(300, 80)
        Me.Controls.Add(Me.lblProgress)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "ProgressForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "处理进度"
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents lblProgress As Label
End Class
