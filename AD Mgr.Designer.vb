<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form 重写 Dispose，以清理组件列表。
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    ' Windows 窗体设计器所必需的
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.txtJobNumber = New System.Windows.Forms.TextBox()
        Me.txtComputerName = New System.Windows.Forms.TextBox()
        Me.txtAssetNumber = New System.Windows.Forms.TextBox()
        Me.lblJobNumber = New System.Windows.Forms.Label()
        Me.lblComputerName = New System.Windows.Forms.Label()
        Me.lblAssetNumber = New System.Windows.Forms.Label()
        Me.btnCheckJob = New System.Windows.Forms.Button()
        Me.btnCheckComputer = New System.Windows.Forms.Button()
        Me.btnJoinDomain = New System.Windows.Forms.Button()
        Me.btnChangeName = New System.Windows.Forms.Button()
        Me.btnSettings = New System.Windows.Forms.Button()
        Me.btnUnjoinDomain = New System.Windows.Forms.Button()
        Me.lblSecret = New System.Windows.Forms.Label()
        Me.lblDomainStatus = New System.Windows.Forms.Label()
        Me.picStatusBall = New System.Windows.Forms.PictureBox()
        Me.ToolTip1 = New System.Windows.Forms.ToolTip(Me.components)
        CType(Me.picStatusBall, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'txtJobNumber
        '
        Me.txtJobNumber.Location = New System.Drawing.Point(120, 20)
        Me.txtJobNumber.Name = "txtJobNumber"
        Me.txtJobNumber.Size = New System.Drawing.Size(200, 21)
        Me.txtJobNumber.TabIndex = 0
        Me.txtJobNumber.Text = "请输入工号"
        '
        'txtComputerName
        '
        Me.txtComputerName.Location = New System.Drawing.Point(120, 60)
        Me.txtComputerName.Name = "txtComputerName"
        Me.txtComputerName.Size = New System.Drawing.Size(200, 21)
        Me.txtComputerName.TabIndex = 2
        Me.txtComputerName.Text = "请输入计算机名"
        '
        'txtAssetNumber
        '
        Me.txtAssetNumber.Location = New System.Drawing.Point(120, 99)
        Me.txtAssetNumber.Name = "txtAssetNumber"
        Me.txtAssetNumber.Size = New System.Drawing.Size(200, 21)
        Me.txtAssetNumber.TabIndex = 4
        Me.txtAssetNumber.Text = "请输入资产编号"
        '
        'lblJobNumber
        '
        Me.lblJobNumber.AutoSize = True
        Me.lblJobNumber.Location = New System.Drawing.Point(30, 23)
        Me.lblJobNumber.Name = "lblJobNumber"
        Me.lblJobNumber.Size = New System.Drawing.Size(41, 12)
        Me.lblJobNumber.TabIndex = 5
        Me.lblJobNumber.Text = "工号："
        '
        'lblComputerName
        '
        Me.lblComputerName.AutoSize = True
        Me.lblComputerName.Location = New System.Drawing.Point(30, 63)
        Me.lblComputerName.Name = "lblComputerName"
        Me.lblComputerName.Size = New System.Drawing.Size(65, 12)
        Me.lblComputerName.TabIndex = 6
        Me.lblComputerName.Text = "计算机名："
        '
        'lblAssetNumber
        '
        Me.lblAssetNumber.AutoSize = True
        Me.lblAssetNumber.Location = New System.Drawing.Point(30, 102)
        Me.lblAssetNumber.Name = "lblAssetNumber"
        Me.lblAssetNumber.Size = New System.Drawing.Size(65, 12)
        Me.lblAssetNumber.TabIndex = 7
        Me.lblAssetNumber.Text = "资产编号："
        '
        'btnCheckJob
        '
        Me.btnCheckJob.FlatStyle = System.Windows.Forms.FlatStyle.System
        Me.btnCheckJob.Location = New System.Drawing.Point(330, 19)
        Me.btnCheckJob.Name = "btnCheckJob"
        Me.btnCheckJob.Size = New System.Drawing.Size(35, 23)
        Me.btnCheckJob.TabIndex = 1
        Me.btnCheckJob.Text = "检测"
        Me.btnCheckJob.UseVisualStyleBackColor = True
        '
        'btnCheckComputer
        '
        Me.btnCheckComputer.FlatStyle = System.Windows.Forms.FlatStyle.System
        Me.btnCheckComputer.Location = New System.Drawing.Point(330, 59)
        Me.btnCheckComputer.Name = "btnCheckComputer"
        Me.btnCheckComputer.Size = New System.Drawing.Size(35, 23)
        Me.btnCheckComputer.TabIndex = 3
        Me.btnCheckComputer.Text = "检测"
        Me.btnCheckComputer.UseVisualStyleBackColor = True
        '
        'btnJoinDomain
        '
        Me.btnJoinDomain.FlatStyle = System.Windows.Forms.FlatStyle.System
        Me.btnJoinDomain.Location = New System.Drawing.Point(35, 143)
        Me.btnJoinDomain.Name = "btnJoinDomain"
        Me.btnJoinDomain.Size = New System.Drawing.Size(100, 30)
        Me.btnJoinDomain.TabIndex = 8
        Me.btnJoinDomain.Text = "加域"
        Me.btnJoinDomain.UseVisualStyleBackColor = True
        '
        'btnChangeName
        '
        Me.btnChangeName.FlatStyle = System.Windows.Forms.FlatStyle.System
        Me.btnChangeName.Location = New System.Drawing.Point(140, 143)
        Me.btnChangeName.Name = "btnChangeName"
        Me.btnChangeName.Size = New System.Drawing.Size(100, 30)
        Me.btnChangeName.TabIndex = 9
        Me.btnChangeName.Text = "更换域账号"
        Me.btnChangeName.UseVisualStyleBackColor = True
        '
        'btnSettings
        '
        Me.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.System
        Me.btnSettings.Location = New System.Drawing.Point(245, 143)
        Me.btnSettings.Name = "btnSettings"
        Me.btnSettings.Size = New System.Drawing.Size(100, 30)
        Me.btnSettings.TabIndex = 10
        Me.btnSettings.Text = "设置"
        Me.btnSettings.UseVisualStyleBackColor = True
        '
        'btnUnjoinDomain
        '
        Me.btnUnjoinDomain.FlatStyle = System.Windows.Forms.FlatStyle.System
        Me.btnUnjoinDomain.Location = New System.Drawing.Point(140, 181)
        Me.btnUnjoinDomain.Name = "btnUnjoinDomain"
        Me.btnUnjoinDomain.Size = New System.Drawing.Size(100, 30)
        Me.btnUnjoinDomain.TabIndex = 11
        Me.btnUnjoinDomain.Text = "退出域"
        Me.btnUnjoinDomain.UseVisualStyleBackColor = True
        Me.btnUnjoinDomain.Visible = False
        '
        'lblSecret
        '
        Me.lblSecret.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.lblSecret.Location = New System.Drawing.Point(290, 190)
        Me.lblSecret.Name = "lblSecret"
        Me.lblSecret.Size = New System.Drawing.Size(70, 12)
        Me.lblSecret.TabIndex = 12
        Me.lblSecret.Text = "版本：v2.4"
        '
        'lblDomainStatus
        '
        Me.lblDomainStatus.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.lblDomainStatus.AutoSize = True
        Me.lblDomainStatus.Location = New System.Drawing.Point(12, 192)
        Me.lblDomainStatus.Name = "lblDomainStatus"
        Me.lblDomainStatus.Size = New System.Drawing.Size(29, 12)
        Me.lblDomainStatus.TabIndex = 13
        Me.lblDomainStatus.Text = "状态"
        '
        'picStatusBall
        '
        Me.picStatusBall.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.picStatusBall.BackColor = System.Drawing.Color.Transparent
        Me.picStatusBall.Location = New System.Drawing.Point(48, 192)
        Me.picStatusBall.Name = "picStatusBall"
        Me.picStatusBall.Size = New System.Drawing.Size(12, 12)
        Me.picStatusBall.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize
        Me.picStatusBall.TabIndex = 14
        Me.picStatusBall.TabStop = False
        '
        'ToolTip1
        '
        Me.ToolTip1.IsBalloon = True
        Me.ToolTip1.ToolTipTitle = "加域状态"
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(381, 220)
        Me.Controls.Add(Me.picStatusBall)
        Me.Controls.Add(Me.lblDomainStatus)
        Me.Controls.Add(Me.lblSecret)
        Me.Controls.Add(Me.btnUnjoinDomain)
        Me.Controls.Add(Me.btnSettings)
        Me.Controls.Add(Me.btnChangeName)
        Me.Controls.Add(Me.btnJoinDomain)
        Me.Controls.Add(Me.lblAssetNumber)
        Me.Controls.Add(Me.txtAssetNumber)
        Me.Controls.Add(Me.lblComputerName)
        Me.Controls.Add(Me.lblJobNumber)
        Me.Controls.Add(Me.txtComputerName)
        Me.Controls.Add(Me.btnCheckComputer)
        Me.Controls.Add(Me.txtJobNumber)
        Me.Controls.Add(Me.btnCheckJob)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "Form1"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "AD域管理工具"
        CType(Me.picStatusBall, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents txtJobNumber As TextBox
    Friend WithEvents txtComputerName As TextBox
    Friend WithEvents txtAssetNumber As TextBox
    Friend WithEvents lblJobNumber As Label
    Friend WithEvents lblComputerName As Label
    Friend WithEvents lblAssetNumber As Label
    Friend WithEvents btnJoinDomain As Button
    Friend WithEvents btnChangeName As Button
    Friend WithEvents btnSettings As Button
    Friend WithEvents btnUnjoinDomain As Button
    Friend WithEvents lblSecret As Label
    Friend WithEvents lblDomainStatus As Label
    Friend WithEvents picStatusBall As PictureBox
    Friend WithEvents btnCheckJob As Button
    Friend WithEvents btnCheckComputer As Button
    Friend WithEvents ToolTip1 As ToolTip

End Class
