Imports System.Windows.Forms
Imports System.DirectoryServices
Imports System.Net.NetworkInformation

Public Class SettingsForm

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub SettingsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LoadSettingsToUi()
    End Sub

    Private Sub LoadSettingsToUi()
        Dim cfg = ConfigHelper.LoadConfig()
        If cfg.ContainsKey("Domain") Then txtDomain.Text = cfg("Domain")
        If cfg.ContainsKey("User") Then txtUser.Text = cfg("User")
        If cfg.ContainsKey("Password") Then txtPassword.Text = ConfigHelper.Decrypt(cfg("Password"))
        If cfg.ContainsKey("OU") Then txtOU.Text = cfg("OU")

        If cfg.ContainsKey("AddDomainToLocalAdmin") Then
            Dim v = cfg("AddDomainToLocalAdmin").ToString().Trim().ToLowerInvariant()
            chkAddDomainToLocalAdmin.Checked = (v = "true" OrElse v = "1" OrElse v = "yes")
        Else
            chkAddDomainToLocalAdmin.Checked = True
        End If
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs)
        Dim domain = txtDomain.Text.Trim()
        Dim user = txtUser.Text.Trim()
        Dim pwd = txtPassword.Text ' 明文，由 ConfigHelper 加密保存
        Dim ou = txtOU.Text.Trim()
        Dim addToLocal = chkAddDomainToLocalAdmin.Checked

        If String.IsNullOrWhiteSpace(domain) Then
            MessageBox.Show("请填写域名（Domain）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtDomain.Focus()
            Return
        End If

        Try
            ' 使用域名作为域控地址（对于大多数AD环境，域名即可用于LDAP绑定）
            ConfigHelper.SaveConfig(domain, user, pwd, ou, domain, addToLocal)
            MessageBox.Show("设置已保存。", "已保存", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.DialogResult = DialogResult.OK
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("保存设置失败：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnCancel_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.Cancel
        Me.Close()
    End Sub

    ' 仅通过域名测试（不再使用域控地址）
    Private Sub btnTestConnection_Click(sender As Object, e As EventArgs)
        Dim domain = txtDomain.Text.Trim()
        If String.IsNullOrWhiteSpace(domain) Then
            MessageBox.Show("请先填写域名（Domain）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim user = txtUser.Text.Trim()
        Dim pwd = txtPassword.Text

        btnTestConnection.Enabled = False
        btnTestConnection.Text = "测试中..."
        Application.DoEvents()

        Try
            ' 1) Ping 域名（依赖 DNS）
            Dim pingOk As Boolean = False
            Try
                Using p As New Ping()
                    Dim reply = p.Send(domain, 1500)
                    pingOk = (reply IsNot Nothing AndAlso reply.Status = IPStatus.Success)
                End Using
            Catch exPing As Exception
                pingOk = False
            End Try

            If Not pingOk Then
                MessageBox.Show("无法 Ping 通域名：" & domain & "。请确认 DNS/网络 配置。", "测试结果", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            ' 2) LDAP 绑定测试（使用域名进行 LDAP 绑定）
            If String.IsNullOrWhiteSpace(user) OrElse String.IsNullOrWhiteSpace(pwd) Then
                MessageBox.Show("Ping 成功，但未提供域管理员账号/密码，跳过 LDAP 验证。", "测试结果", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim ldapOk As Boolean = False
            Dim ldapErr As String = String.Empty
            Dim ldapPath As String = "LDAP://" & domain
            Dim ldapUser As String = user
            Dim ldapPwd As String = pwd

            ' 尝试改进用户名格式
            If Not user.Contains("@") AndAlso Not user.Contains("\") Then
                ' 如果用户名既不包含 @ 也不包含 \，尝试添加域名前缀
                ldapUser = domain & "\" & user
            End If

            Try
                Using entry As New DirectoryEntry(ldapPath, ldapUser, ldapPwd, AuthenticationTypes.Secure)
                    ' 强制触发认证
                    Dim name As String = entry.Name
                    ldapOk = True
                End Using
            Catch exLdap As Exception
                ldapOk = False
                ldapErr = exLdap.Message
                
                ' 如果第一次尝试失败，尝试使用 @ 格式
                If Not user.Contains("@") AndAlso user.Contains("\") Then
                    Try
                        Dim parts = user.Split("\"c)
                        If parts.Length = 2 Then
                            ldapUser = parts(1) & "@" & domain
                            Using entry As New DirectoryEntry(ldapPath, ldapUser, ldapPwd, AuthenticationTypes.Secure)
                                Dim name As String = entry.Name
                                ldapOk = True
                            End Using
                        End If
                    Catch exRetry As Exception
                        ' 保持第一次的错误信息
                    End Try
                End If
            End Try

            If ldapOk Then
                MessageBox.Show("测试通过：已能 Ping 通域名，且能使用提供的域管理员凭据绑定 LDAP。", "测试结果", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                MessageBox.Show("Ping 成功，但 LDAP 绑定/查询失败：" & ldapErr & vbCrLf & vbCrLf & "提示：" & vbCrLf & "- 请确认账号格式是否正确（DOMAIN\username 或 username@domain.com）" & vbCrLf & "- 请确保账号具有域管理员权限" & vbCrLf & "- 请检查账号密码是否包含特殊字符", "测试结果", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        Finally
            btnTestConnection.Enabled = True
            btnTestConnection.Text = "测试连接"
        End Try
    End Sub
End Class
