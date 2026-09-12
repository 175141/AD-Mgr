<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class SettingsForm
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer
    Private lblDomain As System.Windows.Forms.Label
    Private txtDomain As System.Windows.Forms.TextBox
    Private lblUser As System.Windows.Forms.Label
    Private txtUser As System.Windows.Forms.TextBox
    Private lblPassword As System.Windows.Forms.Label
    Private txtPassword As System.Windows.Forms.TextBox
    Private lblOU As System.Windows.Forms.Label
    Private txtOU As System.Windows.Forms.TextBox
    Private chkAddDomainToLocalAdmin As System.Windows.Forms.CheckBox
    Private btnSave As System.Windows.Forms.Button
    Private btnCancel As System.Windows.Forms.Button
    Private btnTestConnection As System.Windows.Forms.Button

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.lblDomain = New System.Windows.Forms.Label()
        Me.txtDomain = New System.Windows.Forms.TextBox()
        Me.lblUser = New System.Windows.Forms.Label()
        Me.txtUser = New System.Windows.Forms.TextBox()
        Me.lblPassword = New System.Windows.Forms.Label()
        Me.txtPassword = New System.Windows.Forms.TextBox()
        Me.lblOU = New System.Windows.Forms.Label()
        Me.txtOU = New System.Windows.Forms.TextBox()
        Me.chkAddDomainToLocalAdmin = New System.Windows.Forms.CheckBox()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.btnTestConnection = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblDomain
        '
        Me.lblDomain.AutoSize = True
        Me.lblDomain.Location = New System.Drawing.Point(12, 15)
        Me.lblDomain.Name = "lblDomain"
        Me.lblDomain.Size = New System.Drawing.Size(83, 12)
        Me.lblDomain.TabIndex = 0
        Me.lblDomain.Text = "域名 (Domain)"
        '
        'txtDomain
        '
        Me.txtDomain.Location = New System.Drawing.Point(140, 12)
        Me.txtDomain.Name = "txtDomain"
        Me.txtDomain.Size = New System.Drawing.Size(300, 21)
        Me.txtDomain.TabIndex = 1
        '
        'lblUser
        '
        Me.lblUser.AutoSize = True
        Me.lblUser.Location = New System.Drawing.Point(12, 48)
        Me.lblUser.Name = "lblUser"
        Me.lblUser.Size = New System.Drawing.Size(95, 12)
        Me.lblUser.TabIndex = 2
        Me.lblUser.Text = "域管理员 (User)"
        '
        'txtUser
        '
        Me.txtUser.Location = New System.Drawing.Point(140, 45)
        Me.txtUser.Name = "txtUser"
        Me.txtUser.Size = New System.Drawing.Size(300, 21)
        Me.txtUser.TabIndex = 3
        '
        'lblPassword
        '
        Me.lblPassword.AutoSize = True
        Me.lblPassword.Location = New System.Drawing.Point(12, 81)
        Me.lblPassword.Name = "lblPassword"
        Me.lblPassword.Size = New System.Drawing.Size(95, 12)
        Me.lblPassword.TabIndex = 4
        Me.lblPassword.Text = "密码 (Password)"
        '
        'txtPassword
        '
        Me.txtPassword.Location = New System.Drawing.Point(140, 78)
        Me.txtPassword.Name = "txtPassword"
        Me.txtPassword.Size = New System.Drawing.Size(300, 21)
        Me.txtPassword.TabIndex = 5
        Me.txtPassword.UseSystemPasswordChar = True
        '
        'lblOU
        '
        Me.lblOU.AutoSize = True
        Me.lblOU.Location = New System.Drawing.Point(12, 114)
        Me.lblOU.Name = "lblOU"
        Me.lblOU.Size = New System.Drawing.Size(17, 12)
        Me.lblOU.TabIndex = 6
        Me.lblOU.Text = "OU"
        '
        'txtOU
        '
        Me.txtOU.Location = New System.Drawing.Point(140, 111)
        Me.txtOU.Name = "txtOU"
        Me.txtOU.Size = New System.Drawing.Size(300, 21)
        Me.txtOU.TabIndex = 7
        '
        'chkAddDomainToLocalAdmin
        '
        Me.chkAddDomainToLocalAdmin.AutoSize = True
        Me.chkAddDomainToLocalAdmin.Location = New System.Drawing.Point(92, 144)
        Me.chkAddDomainToLocalAdmin.Name = "chkAddDomainToLocalAdmin"
        Me.chkAddDomainToLocalAdmin.Size = New System.Drawing.Size(348, 16)
        Me.chkAddDomainToLocalAdmin.TabIndex = 8
        Me.chkAddDomainToLocalAdmin.Text = "是否将域用户添加到本地管理员组 (AddDomainToLocalAdmin)"
        Me.chkAddDomainToLocalAdmin.UseVisualStyleBackColor = True
        '
        'btnTestConnection
        '
        Me.btnTestConnection.Location = New System.Drawing.Point(140, 170)
        Me.btnTestConnection.Name = "btnTestConnection"
        Me.btnTestConnection.Size = New System.Drawing.Size(100, 26)
        Me.btnTestConnection.TabIndex = 9
        Me.btnTestConnection.Text = "测试连接"
        Me.btnTestConnection.UseVisualStyleBackColor = True
        '
        'btnSave
        '
        Me.btnSave.Location = New System.Drawing.Point(284, 170)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(75, 26)
        Me.btnSave.TabIndex = 10
        Me.btnSave.Text = "保存"
        Me.btnSave.UseVisualStyleBackColor = True
        '
        'btnCancel
        '
        Me.btnCancel.Location = New System.Drawing.Point(365, 170)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(75, 26)
        Me.btnCancel.TabIndex = 11
        Me.btnCancel.Text = "取消"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        ' 将事件处理器绑定到按钮（AddHandler）
        '
        AddHandler Me.btnSave.Click, AddressOf Me.btnSave_Click
        AddHandler Me.btnCancel.Click, AddressOf Me.btnCancel_Click
        AddHandler Me.btnTestConnection.Click, AddressOf Me.btnTestConnection_Click
        '
        'SettingsForm
        '
        Me.ClientSize = New System.Drawing.Size(454, 210)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnSave)
        Me.Controls.Add(Me.btnTestConnection)
        Me.Controls.Add(Me.chkAddDomainToLocalAdmin)
        Me.Controls.Add(Me.txtOU)
        Me.Controls.Add(Me.lblOU)
        Me.Controls.Add(Me.txtPassword)
        Me.Controls.Add(Me.lblPassword)
        Me.Controls.Add(Me.txtUser)
        Me.Controls.Add(Me.lblUser)
        Me.Controls.Add(Me.txtDomain)
        Me.Controls.Add(Me.lblDomain)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "SettingsForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "设置"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub
End Class