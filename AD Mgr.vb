Imports System.DirectoryServices
Imports Microsoft.Win32
Imports System.Threading
Imports System.Security.Principal
Imports System.IO
Imports System.Windows.Forms
Imports System.Management
Imports System.Net.NetworkInformation
Imports System.ComponentModel

Public Class Form1

    ' 标记：用户是否手动编辑了计算机名（若为 True 则不再自动同步）
    Private manualComputerNameEdit As Boolean = False
    ' 程序内部设置计算机名时用来抑制 TextChanged 对手动标记的影响
    Private suppressComputerNameUserFlag As Boolean = False

    ' 标记：用户是否手动点击了检测按钮（禁用自动检测）
    Private disableAutoCheck As Boolean = False

    ' 输入空闲检测定时器（工号、计算机名）
    Private jobIdleTimer As System.Windows.Forms.Timer
    Private compIdleTimer As System.Windows.Forms.Timer
    ' 修改为 3000ms（3 秒）
    Private Const InputIdleMs As Integer = 3000

    ' 缓存上次检测值，避免重复提示
    Private lastJobChecked As String = String.Empty
    Private lastCompChecked As String = String.Empty

    Private Sub CheckAdmin()
        Dim principal = New WindowsPrincipal(WindowsIdentity.GetCurrent())
        If Not principal.IsInRole(WindowsBuiltInRole.Administrator) Then
            MessageBox.Show("请以管理员身份运行本程序！", "权限不足", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Application.Exit()
        End If
    End Sub

    ' 将 NET API 返回码转换为可读信息
    Private Function NetApiErrorMessage(code As Integer) As String
        Try
            Return New Win32Exception(code).Message & " (代码: " & code & ")"
        Catch
            Return "未知错误，代码: " & code
        End Try
    End Function

    ' 从配置读取域控地址（优先 key "DomainController"，否则回退到 "Domain"；如果都不存在返回空字符串）
    Private Function GetDomainControllerAddress() As String
        Try
            Dim cfg = ConfigHelper.LoadConfig()
            If cfg IsNot Nothing Then
                If cfg.ContainsKey("DomainController") AndAlso Not String.IsNullOrWhiteSpace(cfg("DomainController")) Then
                    Return cfg("DomainController").Trim()
                End If
                If cfg.ContainsKey("Domain") AndAlso Not String.IsNullOrWhiteSpace(cfg("Domain")) Then
                    Return cfg("Domain").Trim()
                End If
            End If
        Catch ex As Exception
            Return String.Empty
        End Try

        ' 找不到配置时返回空字符串（调用方必须处理）
        Return String.Empty
    End Function

    ' 简单 Ping 检查网络连通性
    Private Function PingHost(ip As String, timeoutMs As Integer) As Boolean
        Try
            Using p As New Ping()
                Dim reply = p.Send(ip, timeoutMs)
                Return (reply IsNot Nothing AndAlso reply.Status = IPStatus.Success)
            End Using
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 判断是否为笔记本（优先使用 SystemInformation，其次回退到 WMI）
    Private Function IsLaptop() As Boolean
        Try
            Dim status As System.Windows.Forms.PowerStatus = SystemInformation.PowerStatus
            Return status.BatteryChargeStatus <> BatteryChargeStatus.NoSystemBattery
        Catch
            Try
                Using searcher As New System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Battery")
                    Dim collection = searcher.Get()
                    Return collection.Count > 0
                End Using
            Catch
                Return False
            End Try
        End Try
    End Function

    ' 根据工号生成计算机名（笔记本使用 -N 后缀）
    Private Function GenerateComputerNameFromJob(jobNumber As String) As String
        If String.IsNullOrWhiteSpace(jobNumber) Then Return String.Empty
        Dim baseName = jobNumber.Trim().ToUpperInvariant()
        baseName = System.Text.RegularExpressions.Regex.Replace(baseName, "[^\w\-]", "")

        If IsLaptop() Then
            If baseName.EndsWith("-N", StringComparison.OrdinalIgnoreCase) Then
                ' 已包含 -N，保持不变
            ElseIf baseName.EndsWith("N", StringComparison.OrdinalIgnoreCase) Then
                baseName = baseName.Substring(0, baseName.Length - 1) & "-N"
            Else
                baseName &= "-N"
            End If
        End If

        If baseName.Length > 15 Then
            baseName = baseName.Substring(0, 15)
        End If
        Return baseName
    End Function

    ' 检查本机是否已加入任意域（返回 True 表示已加域）
    Private Function IsDomainJoined() As Boolean
        Try
            Dim domain = System.DirectoryServices.ActiveDirectory.Domain.GetComputerDomain()
            Return domain IsNot Nothing
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 在指定域控（使用 LDAP 绑定到 IP/域名）上检查计算机对象是否存在（sAMAccountName 带 $）
    Private Function ComputerExistsOnDC(dcIp As String, computerName As String, domainAdmin As String, domainPwd As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(dcIp) OrElse String.IsNullOrWhiteSpace(computerName) Then Return False

            ' 生成多种可能的账号格式进行尝试
            Dim authFormats As New List(Of String) From {domainAdmin}

            If Not domainAdmin.Contains("@") AndAlso Not domainAdmin.Contains("\") Then
                authFormats.Add(dcIp & "\" & domainAdmin)
                authFormats.Add(domainAdmin & "@" & dcIp)
            ElseIf domainAdmin.Contains("\") Then
                Dim parts = domainAdmin.Split("\"c)
                If parts.Length = 2 Then
                    authFormats.Add(parts(1) & "@" & dcIp)
                End If
            End If

            ' 尝试不同的认证类型
            Dim authTypes As New List(Of AuthenticationTypes) From {
                AuthenticationTypes.Secure,
                AuthenticationTypes.None,
                AuthenticationTypes.Secure Or AuthenticationTypes.Sealing
            }

            For Each authFormat In authFormats
                For Each authType In authTypes
                    Try

                        Using root As New DirectoryEntry("LDAP://" & dcIp, authFormat, domainPwd, authType)
                            Using searcher As New DirectorySearcher(root)
                                searcher.Filter = "(&(objectCategory=computer)(sAMAccountName=" & computerName & "$))"
                                searcher.SearchScope = SearchScope.Subtree
                                searcher.PropertiesToLoad.Add("cn")
                                Dim res = searcher.FindOne()
                                If res IsNot Nothing Then
                                    Return True
                                End If
                            End Using
                        End Using
                    Catch ex As Exception
                    End Try
                Next
            Next

            Return False
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 在域控上检查用户（工号）是否存在（sAMAccountName）
    Private Function UserExistsOnDC(dcIp As String, jobNumber As String, domainAdmin As String, domainPwd As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(dcIp) OrElse String.IsNullOrWhiteSpace(jobNumber) Then Return False

            ' 生成多种可能的账号格式进行尝试
            Dim authFormats As New List(Of String) From {domainAdmin}

            ' 如果账号不包含 @ 和 \，生成其他格式
            If Not domainAdmin.Contains("@") AndAlso Not domainAdmin.Contains("\") Then
                authFormats.Add(dcIp & "\" & domainAdmin)  ' DOMAIN\username
                authFormats.Add(domainAdmin & "@" & dcIp)    ' username@domain.com
            ElseIf domainAdmin.Contains("\") Then
                ' 如果是 DOMAIN\username 格式，也尝试 username@domain.com 格式
                Dim parts = domainAdmin.Split("\"c)
                If parts.Length = 2 Then
                    authFormats.Add(parts(1) & "@" & dcIp)
                End If
            End If

            ' 尝试不同的认证类型
            Dim authTypes As New List(Of AuthenticationTypes) From {
                AuthenticationTypes.Secure,
                AuthenticationTypes.None,
                AuthenticationTypes.Secure Or AuthenticationTypes.Sealing
            }

            For Each authFormat In authFormats
                For Each authType In authTypes
                    Try

                        Using root As New DirectoryEntry("LDAP://" & dcIp, authFormat, domainPwd, authType)
                            ' 强制触发认证
                            Dim name = root.Name

                            Using searcher As New DirectorySearcher(root)
                                searcher.Filter = "(&(objectCategory=person)(objectClass=user)(sAMAccountName=" & jobNumber & "))"
                                searcher.SearchScope = SearchScope.Subtree
                                searcher.PropertiesToLoad.Add("cn")
                                Dim res = searcher.FindOne()
                                If res IsNot Nothing Then
                                    Return True
                                End If
                            End Using
                        End Using
                    Catch ex As Exception
                    End Try
                Next
            Next

            ' 如果都失败，返回False
            Return False
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 删除域控上指定计算机对象（存在时），返回 True 表示已删除或不存在
    Private Function DeleteComputerOnDC(dcIp As String, computerName As String, domainAdmin As String, domainPwd As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(dcIp) OrElse String.IsNullOrWhiteSpace(computerName) Then Return True

            ' 生成多种可能的账号格式进行尝试
            Dim authFormats As New List(Of String) From {domainAdmin}

            If Not domainAdmin.Contains("@") AndAlso Not domainAdmin.Contains("\") Then
                authFormats.Add(dcIp & "\" & domainAdmin)
                authFormats.Add(domainAdmin & "@" & dcIp)
            ElseIf domainAdmin.Contains("\") Then
                Dim parts = domainAdmin.Split("\"c)
                If parts.Length = 2 Then
                    authFormats.Add(parts(1) & "@" & dcIp)
                End If
            End If

            ' 尝试不同的认证类型
            Dim authTypes As New List(Of AuthenticationTypes) From {
                AuthenticationTypes.Secure,
                AuthenticationTypes.None,
                AuthenticationTypes.Secure Or AuthenticationTypes.Sealing
            }

            For Each authFormat In authFormats
                For Each authType In authTypes
                    Try

                        Using root As New DirectoryEntry("LDAP://" & dcIp, authFormat, domainPwd, authType)
                            Using searcher As New DirectorySearcher(root)
                                searcher.Filter = "(&(objectCategory=computer)(sAMAccountName=" & computerName & "$))"
                                searcher.SearchScope = SearchScope.Subtree
                                Dim res = searcher.FindOne()
                                If res Is Nothing Then
                                    Return True
                                End If

                                Using de As New DirectoryEntry(res.Path, authFormat, domainPwd, authType)
                                    Dim parent As DirectoryEntry = de.Parent
                                    If parent IsNot Nothing Then
                                        parent.Children.Remove(de)
                                        parent.CommitChanges()
                                        Return True
                                    Else
                                        Try
                                            de.DeleteTree()
                                            de.CommitChanges()
                                            Return True
                                        Catch exDelete As Exception
                                            Return False
                                        End Try
                                    End If
                                End Using
                            End Using
                        End Using
                    Catch ex As Exception
                    End Try
                Next
            Next

            Return False
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 检查本地 Administrators 组是否已包含域/工号帐号（返回 True 表示已存在）
    Private Function LocalAdminHasDomainUser(jobNumber As String, domain As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(jobNumber) OrElse String.IsNullOrWhiteSpace(domain) Then Return False
            Dim computerName = Environment.MachineName
            Dim group = New DirectoryEntry($"WinNT://{computerName}/Administrators,group")
            Dim memberPath = $"WinNT://{domain}/{jobNumber}"
            Try
                Dim isMemberObj = group.Invoke("IsMember", New Object() {memberPath})
                If isMemberObj IsNot Nothing Then
                    Return Convert.ToBoolean(isMemberObj)
                End If
            Catch exInner As Exception
                Try
                    Dim members = CType(group.Invoke("Members"), System.Collections.IEnumerable)
                    For Each m In members
                        Try
                            Dim de As DirectoryEntry = New DirectoryEntry(m)
                            Dim adsPath = de.Path
                            If adsPath.EndsWith("/" & jobNumber, StringComparison.OrdinalIgnoreCase) OrElse adsPath.EndsWith("\" & jobNumber, StringComparison.OrdinalIgnoreCase) Then
                                If adsPath.IndexOf("/" & domain, StringComparison.OrdinalIgnoreCase) >= 0 OrElse adsPath.IndexOf("\" & domain, StringComparison.OrdinalIgnoreCase) >= 0 Then
                                    Return True
                                End If
                            End If
                        Catch exMember As Exception
                            ' Skip
                        End Try
                    Next
                Catch exMembers As Exception
                    ' Skip
                End Try
            End Try

            Return False
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 在添加域用户到本地管理员组成功后，移除当前登录的域用户（如果是域用户且非本地用户），仅从本地管理员组中移除
    Private Sub RemoveCurrentDomainUserFromLocalAdmins()
        Try
            Dim id = WindowsIdentity.GetCurrent()
            If id Is Nothing Then Return
            Dim fullName = id.Name ' e.g. "DOMAIN\username" or "MACHINE\username"
            If String.IsNullOrWhiteSpace(fullName) Then Return
            If Not fullName.Contains("\") Then Return

            Dim parts = fullName.Split("\"c)
            If parts.Length <> 2 Then Return
            Dim userDomain = parts(0)
            Dim userName = parts(1)

            Dim machineName = Environment.MachineName
            If String.Compare(userDomain, machineName, True) = 0 Then
                Return
            End If

            Try
                Dim comp = Environment.MachineName
                Dim group As New DirectoryEntry($"WinNT://{comp}/Administrators,group")
                Dim memberPath = $"WinNT://{userDomain}/{userName}"
                Try
                    group.Invoke("Remove", New Object() {memberPath})
                Catch exRemove As Exception
                    ' 可能不在组中，忽略
                End Try
            Catch ex As Exception
                ' Skip
            End Try
        Catch ex As Exception
            ' Skip
        End Try
    End Sub

    ' 显示 30 秒倒计时重启窗口（含“立即重启”和“稍后重启”按钮）
    Private Sub ShowRestartCountdown(seconds As Integer)
        Dim frm = New Form With {
            .StartPosition = FormStartPosition.CenterParent,
            .Size = New Drawing.Size(360, 150),
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox = False,
            .MinimizeBox = False,
            .Text = "重启确认"
        }

        Dim lbl = New Label With {
            .AutoSize = False,
            .TextAlign = Drawing.ContentAlignment.MiddleCenter,
            .Dock = DockStyle.Top,
            .Height = 60,
            .Font = New Drawing.Font("微软雅黑", 11),
            .Text = "将在 " & seconds & " 秒后自动重启..."
        }
        frm.Controls.Add(lbl)

        Dim btnNow = New Button With {
            .Text = "立即重启",
            .Width = 100,
            .Height = 30,
            .Left = 60,
            .Top = 80
        }
        frm.Controls.Add(btnNow)

        Dim btnLater = New Button With {
            .Text = "稍后重启",
            .Width = 100,
            .Height = 30,
            .Left = frm.ClientSize.Width - 160,
            .Top = 80
        }
        frm.Controls.Add(btnLater)

        Dim t = New System.Windows.Forms.Timer With {.Interval = 1000}
        Dim remaining = seconds

        AddHandler t.Tick, Sub()
                               remaining -= 1
                               If remaining <= 0 Then
                                   t.Stop()
                                   frm.Close()
                                   Try
                                       Process.Start("shutdown", "/r /t 0")
                                   Catch ex As Exception
                                       ' Ignore
                                   End Try
                               Else
                                   lbl.Text = "将在 " & remaining & " 秒后自动重启..."
                               End If
                           End Sub

        AddHandler btnNow.Click, Sub()
                                     t.Stop()
                                     frm.Close()
                                     Try
                                         Process.Start("shutdown", "/r /t 0")
                                     Catch ex As Exception
                                         ' Ignore
                                     End Try
                                 End Sub

        AddHandler btnLater.Click, Sub()
                                       t.Stop()
                                       frm.Close()
                                   End Sub

        t.Start()
        frm.ShowDialog(Me)
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        CheckAdmin()

        ' 初始化输入完成定时器（用户停止输入 3 秒视为完成）
        jobIdleTimer = New System.Windows.Forms.Timer() With {.Interval = InputIdleMs}
        AddHandler jobIdleTimer.Tick, AddressOf JobIdleTimer_Tick

        compIdleTimer = New System.Windows.Forms.Timer() With {.Interval = InputIdleMs}
        AddHandler compIdleTimer.Tick, AddressOf CompIdleTimer_Tick

        ' 设置界面标题与按钮文字（防止 Designer 未更新）
        Me.Text = "AD域管理工具"
        If Me.Controls.ContainsKey("btnJoinDomain") Then
            CType(Me.Controls("btnJoinDomain"), Button).Text = "加域"
        End If
        If Me.Controls.ContainsKey("btnChangeName") Then
            CType(Me.Controls("btnChangeName"), Button).Text = "更换域账号"
            ' 更换域账号按钮不再隐藏（按用户要求）
            CType(Me.Controls("btnChangeName"), Button).Visible = True
        End If

        ' 设置版本显示（右下）
        If Me.Controls.ContainsKey("lblSecret") Then
            CType(Me.Controls("lblSecret"), Label).Text = "版本：v2.4"
        End If

        ' 保证退域按钮默认隐藏（若希望也显示可调整）
        If Me.Controls.ContainsKey("btnUnjoinDomain") Then
            CType(Me.Controls("btnUnjoinDomain"), Button).Visible = False
        End If

        ' 设置加域状态显示
        If Me.Controls.ContainsKey("lblDomainStatus") Then
            Try
                Dim joined = IsDomainJoined()
                CType(Me.Controls("lblDomainStatus"), Label).Text = "状态"
                UpdateStatusBall(If(joined, "joined", "notjoined"))
            Catch ex As Exception
                CType(Me.Controls("lblDomainStatus"), Label).Text = "状态"
                UpdateStatusBall("unknown")
            End Try
        End If

        ' 绑定检测按钮事件
        If Me.Controls.ContainsKey("btnCheckJob") Then
            AddHandler CType(Me.Controls("btnCheckJob"), Button).Click, AddressOf BtnCheckJob_Click
        End If
        If Me.Controls.ContainsKey("btnCheckComputer") Then
            AddHandler CType(Me.Controls("btnCheckComputer"), Button).Click, AddressOf BtnCheckComputer_Click
        End If

        ' 绑定事件
        AddHandler txtJobNumber.Enter, Sub()
                                           If txtJobNumber.Text = "请输入工号" Then txtJobNumber.Text = ""
                                       End Sub
        AddHandler txtJobNumber.TextChanged, AddressOf TxtJobNumber_TextChanged
        ' 新增：按回车在工号框触发动作（加域或更换域账号）
        AddHandler txtJobNumber.KeyDown, AddressOf TxtJobNumber_KeyDown

        AddHandler txtComputerName.Enter, AddressOf TxtComputerName_Enter
        AddHandler txtComputerName.TextChanged, AddressOf TxtComputerName_TextChanged
        ' 按回车触发一键加域或更换域（计算机名框）
        AddHandler txtComputerName.KeyDown, AddressOf TxtComputerName_KeyDown

        ' 资产编号占位符处理
        AddHandler txtAssetNumber.Enter, Sub()
                                             If txtAssetNumber.Text = "请输入资产编号" Then txtAssetNumber.Text = ""
                                         End Sub
        AddHandler txtAssetNumber.Leave, Sub()
                                             If String.IsNullOrWhiteSpace(txtAssetNumber.Text) Then txtAssetNumber.Text = "请输入资产编号"
                                         End Sub
        ' 按回车触发一键加域或更换域（资产编号框）
        AddHandler txtAssetNumber.KeyDown, AddressOf TxtAssetNumber_KeyDown

        ' 启动时刷新域状态
        RefreshDomainStatus()
    End Sub

    ' 读取资产编号（忽略占位符）
    Private Function GetAssetNumber() As String
        Dim val = txtAssetNumber.Text.Trim()
        If val = "请输入资产编号" Then Return String.Empty
        Return val
    End Function

    ' 将资产编号写入注册表 HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion 的 RegisteredOwner
    Private Sub SetRegisteredOwner(assetNumber As String)
        Try
            If String.IsNullOrWhiteSpace(assetNumber) Then Return
            Using key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable:=True)
                If key IsNot Nothing Then
                    key.SetValue("RegisteredOwner", assetNumber, RegistryValueKind.String)
                End If
            End Using
        Catch ex As Exception
            ' 忽略写入失败，不阻断主流程
        End Try
    End Sub

    ' 资产编号框回车触发动作
    Private Sub TxtAssetNumber_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            Try
                If IsDomainJoined() Then
                    If Me.Controls.ContainsKey("btnChangeName") Then
                        CType(Me.Controls("btnChangeName"), Button).PerformClick()
                    End If
                Else
                    If Me.Controls.ContainsKey("btnJoinDomain") Then
                        CType(Me.Controls("btnJoinDomain"), Button).PerformClick()
                    End If
                End If
            Catch ex As Exception
                ' 忽略回车触发的异常，交由按钮处理
            End Try
        End If
    End Sub

    ' 刷新并验证当前加域状态：
    Private Sub RefreshDomainStatus()
        Try
            ' 先本地判断是否标记为已加域
            If Not IsDomainJoined() Then
                UpdateStatusBall("notjoined")
                Return
            End If

            ' 已本地加入域，尝试使用配置凭据验证域控与计算机对象
            Dim cfg = ConfigHelper.LoadConfig()
            If Not (cfg.ContainsKey("Domain") AndAlso cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password")) Then
                ' 无配置只能显示已加域但无法验证
                UpdateStatusBall("unverified")
                Return
            End If

            Dim domain = cfg("Domain")
            Dim domainAdmin = cfg("User")
            Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))
            Dim dcAddr = GetDomainControllerAddress()

            ' 先检测能否连通域控
            If String.IsNullOrWhiteSpace(dcAddr) Then
                MessageBox.Show("未配置域控地址或域名，请在设置中填写""域控地址（DomainController）""或""域名（Domain）"".", "未配置域控", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                UpdateStatusBall("unverified")
                Return
            End If

            If Not PingHost(dcAddr, 1000) Then
                Dim dr As DialogResult = MessageBox.Show(
                    "检测到本机已加入域，但无法访问域控（网络/DNS 不通或域控不可达）。" & vbCrLf &
                    "请先确保网络与域控可达（在设置中检查域控地址/域名），然后重试。是否现在重试连接？",
                    "域控不可达", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)

                If dr = DialogResult.Yes Then
                    Thread.Sleep(500)
                    If PingHost(dcAddr, 1000) Then
                        RefreshDomainStatus()
                    Else
                        MessageBox.Show("重新检测仍然无法访问域控。请检查设置或网络后重试。", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        UpdateStatusBall("error")
                    End If
                Else
                    UpdateStatusBall("error")
                End If
                Return
            End If

            ' 能连通域控，检查域控上是否存在本机的计算机对象（避免计算机账号被删除）
            Dim compName = Environment.MachineName
            Dim objExists = ComputerExistsOnDC(dcAddr, compName, domainAdmin, domainPwd)
            If objExists Then
                UpdateStatusBall("joined")
            Else
                ' 计算机对象不存在，视为脱域/异常，询问是否退域再加域（只有在能通域控时才尝试）
                Dim dr2 As DialogResult = MessageBox.Show("已检测到已加入域但域控上缺少本机计算机对象，可能导致登录/策略异常。是否尝试先退出域再重新加入？", "域对象缺失", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                If dr2 = DialogResult.Yes Then
                    Dim progressForm As New ProgressForm()
                    Try
                        progressForm.Show()
                        progressForm.SetProgress("正在退出域…")
                        Application.DoEvents()
                        Dim unRes As Integer = DomainTool.UnjoinDomain(domainAdmin, domainPwd)
                        If unRes <> 0 Then
                            progressForm.Close()
                            MessageBox.Show("退出域失败，错误信息：" & NetApiErrorMessage(unRes), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            UpdateStatusBall("error")
                            Return
                        End If

                        progressForm.SetProgress("正在加入域…")
                        Application.DoEvents()
                        Thread.Sleep(300)
                        Dim ouParam As String = Nothing
                        Dim joinRes As Integer = DomainTool.JoinDomain(domain, domainAdmin, domainPwd, ouParam)
                        progressForm.Close()
                        If joinRes = 0 Then
                            MessageBox.Show("重新加入域成功（注意：某些操作可能需要重启生效）。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information)
                            UpdateStatusBall("joined")
                        Else
                            MessageBox.Show("重新加入域失败，错误信息：" & NetApiErrorMessage(joinRes), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            UpdateStatusBall("error")
                        End If
                    Catch ex As Exception
                        If progressForm IsNot Nothing Then progressForm.Close()
                        MessageBox.Show("执行退域/加域操作时发生异常：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        UpdateStatusBall("error")
                    End Try
                Else
                    UpdateStatusBall("error")
                End If
            End If
        Catch ex As Exception
            UpdateStatusBall("unknown")
        End Try
    End Sub

    ' 当工号改变且用户未手动修改计算机名时，同步计算机名（每次工号变化都同步）
    Private Sub TxtJobNumber_TextChanged(sender As Object, e As EventArgs)
        ' 如果用户手动点击了检测按钮，不再触发自动检测
        If disableAutoCheck Then Return

        manualComputerNameEdit = False
        Dim job = txtJobNumber.Text.Trim()
        Dim generated = GenerateComputerNameFromJob(job)
        suppressComputerNameUserFlag = True
        If Not String.IsNullOrWhiteSpace(generated) Then
            txtComputerName.Text = generated
        End If
        suppressComputerNameUserFlag = False

        ' 重置工号输入完成定时器（用户停止输入 3 秒视为完成）
        If jobIdleTimer IsNot Nothing Then
            jobIdleTimer.Stop()
            If Not String.IsNullOrWhiteSpace(job) Then
                jobIdleTimer.Start()
            End If
        End If
    End Sub

    ' 工号输入空闲处理（1 秒无键入视为完成）
    Private Sub JobIdleTimer_Tick(sender As Object, e As EventArgs)
        Try
            jobIdleTimer.Stop()
            Dim job = txtJobNumber.Text.Trim()
            If String.IsNullOrWhiteSpace(job) Then
                lastJobChecked = String.Empty
                txtJobNumber.BackColor = System.Drawing.SystemColors.Window
                Return
            End If

            If job = lastJobChecked Then Return
            lastJobChecked = job

            Dim cfg = ConfigHelper.LoadConfig()
            If cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password") Then
                Dim domainAdmin = cfg("User")
                Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))
                Dim dcAddr = GetDomainControllerAddress()
                If String.IsNullOrWhiteSpace(dcAddr) Then
                    txtJobNumber.BackColor = System.Drawing.SystemColors.Window
                Else
                    ThreadPool.QueueUserWorkItem(Sub()
                                                     Dim exists = UserExistsOnDC(dcAddr, job, domainAdmin, domainPwd)
                                                     Me.BeginInvoke(Sub()
                                                                        If exists Then
                                                                            txtJobNumber.BackColor = System.Drawing.Color.LightGreen
                                                                        Else
                                                                            txtJobNumber.BackColor = System.Drawing.Color.LightPink
                                                                            MessageBox.Show("工号不存在，请检查后重新输入！

调试信息:
域控地址: " & dcAddr & "
管理员账号: " & domainAdmin, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                                                                            txtJobNumber.Focus()
                                                                            txtJobNumber.SelectAll()
                                                                        End If
                                                                    End Sub)
                                                 End Sub)
                End If
            Else
                txtJobNumber.BackColor = System.Drawing.SystemColors.Window
            End If

            ' --- 新增：工号完成后如果程序生成了计算机名，立即触发计算机名的检查（1s 定时器或直接启动） ---
            If Not manualComputerNameEdit Then
                suppressComputerNameUserFlag = True
                Dim genName = GenerateComputerNameFromJob(job)
                txtComputerName.Text = genName
                suppressComputerNameUserFlag = False
            End If

            ' 强制让下一次 comp 检查生效（避免 lastCompChecked 阻止再次检测）
            lastCompChecked = String.Empty
            If compIdleTimer IsNot Nothing Then
                compIdleTimer.Stop()
                If Not String.IsNullOrWhiteSpace(txtComputerName.Text.Trim()) Then
                    compIdleTimer.Start()
                End If
            End If
        Catch ex As Exception
            ' Ignore
        End Try
    End Sub

    ' 监控计算机名文本变化：若非程序设置则认为用户手动编辑，并重置完成定时器
    Private Sub TxtComputerName_TextChanged(sender As Object, e As EventArgs)
        ' 如果用户手动点击了检测按钮，不再触发自动检测
        If disableAutoCheck Then Return

        If Not suppressComputerNameUserFlag Then
            manualComputerNameEdit = True
            If compIdleTimer IsNot Nothing Then
                compIdleTimer.Stop()
                If Not String.IsNullOrWhiteSpace(txtComputerName.Text.Trim()) Then
                    compIdleTimer.Start()
                End If
            End If
        End If
    End Sub

    ' 计算机名输入空闲处理（1 秒无键入视为完成）
    Private Sub CompIdleTimer_Tick(sender As Object, e As EventArgs)
        Try
            compIdleTimer.Stop()
            Dim compName = txtComputerName.Text.Trim()
            If String.IsNullOrWhiteSpace(compName) Then
                lastCompChecked = String.Empty
                txtComputerName.BackColor = System.Drawing.SystemColors.Window
                Return
            End If

            ' 如果与上次相同则跳过
            If compName = lastCompChecked Then Return

            Dim cfg = ConfigHelper.LoadConfig()
            If cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password") Then
                Dim domainAdmin = cfg("User")
                Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))
                Dim dcAddr = GetDomainControllerAddress()
                If String.IsNullOrWhiteSpace(dcAddr) Then
                    txtComputerName.BackColor = System.Drawing.SystemColors.Window
                    Return
                End If

                ' 先把 lastCompChecked 设置为当前值，后续在必要时清空以便重试
                lastCompChecked = compName

                ThreadPool.QueueUserWorkItem(Sub()
                                                 Dim exists = ComputerExistsOnDC(dcAddr, compName, domainAdmin, domainPwd)
                                                 Me.BeginInvoke(Sub()
                                                                    If exists Then
                                                                        txtComputerName.BackColor = System.Drawing.Color.LightPink
                                                                        ' 提示是否覆盖
                                                                        Dim dr = MessageBox.Show("计算机名已存在，是否覆盖？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                                                                        If dr = DialogResult.Yes Then
                                                                            ' 异步删除域控对象并在成功后清空 lastCompChecked，让用户可继续操作或再次检测
                                                                            Dim progressForm As New ProgressForm()
                                                                            progressForm.Show()
                                                                            ThreadPool.QueueUserWorkItem(Sub()
                                                                                                             Dim delOk = DeleteComputerOnDC(dcAddr, compName, domainAdmin, domainPwd)
                                                                                                             Me.BeginInvoke(Sub()
                                                                                                                                progressForm.Close()
                                                                                                                                If delOk Then
                                                                                                                                    MessageBox.Show("域控上同名计算机对象已删除，可继续操作。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                                                                                                    ' 清除缓存强制重新检测/允许后续操作
                                                                                                                                    lastCompChecked = String.Empty
                                                                                                                                    txtComputerName.BackColor = System.Drawing.SystemColors.Window
                                                                                                                                Else
                                                                                                                                    MessageBox.Show("删除域控上同名计算机对象失败，请手动检查或确认权限。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                                                                                                    ' 保持 pink，允许用户修改或重试
                                                                                                                                    lastCompChecked = String.Empty
                                                                                                                                    txtComputerName.BackColor = System.Drawing.Color.LightPink
                                                                                                                                End If
                                                                                                                            End Sub)
                                                                                                         End Sub)
                                                                        Else
                                                                            ' 用户选择不覆盖，把焦点返回计算机名输入框并清空缓存以便后续再检测
                                                                            lastCompChecked = String.Empty
                                                                            txtComputerName.Focus()
                                                                            txtComputerName.SelectAll()
                                                                        End If
                                                                    Else
                                                                        txtComputerName.BackColor = System.Drawing.Color.LightGreen
                                                                    End If
                                                                End Sub)
                                             End Sub)
            Else
                txtComputerName.BackColor = System.Drawing.SystemColors.Window
            End If
        Catch ex As Exception
            ' Ignore
        End Try
    End Sub

    ' 工号检测按钮点击事件
    Private Sub BtnCheckJob_Click(sender As Object, e As EventArgs)
        ' 禁用自动检测
        disableAutoCheck = True

        ' 停止定时器
        If jobIdleTimer IsNot Nothing Then
            jobIdleTimer.Stop()
        End If
        If compIdleTimer IsNot Nothing Then
            compIdleTimer.Stop()
        End If

        Dim job = txtJobNumber.Text.Trim()
        If String.IsNullOrWhiteSpace(job) Then
            MessageBox.Show("请先输入工号！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim cfg = ConfigHelper.LoadConfig()
        If Not (cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password")) Then
            MessageBox.Show("请先在设置中填写域信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim domainAdmin = cfg("User")
        Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))
        Dim dcAddr = GetDomainControllerAddress()

        If String.IsNullOrWhiteSpace(dcAddr) Then
            MessageBox.Show("未配置域控地址或域名，请在设置中填写", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' 立即开始检测
        ThreadPool.QueueUserWorkItem(Sub()
                                         Dim exists = UserExistsOnDC(dcAddr, job, domainAdmin, domainPwd)
                                         Me.BeginInvoke(Sub()
                                                            If exists Then
                                                                txtJobNumber.BackColor = System.Drawing.Color.LightGreen
                                                                MessageBox.Show("工号验证成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                            Else
                                                                txtJobNumber.BackColor = System.Drawing.Color.LightPink
                                                                MessageBox.Show("工号不存在，请检查后重新输入！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                                                                txtJobNumber.Focus()
                                                                txtJobNumber.SelectAll()
                                                            End If
                                                        End Sub)
                                     End Sub)
    End Sub

    ' 计算机名检测按钮点击事件
    Private Sub BtnCheckComputer_Click(sender As Object, e As EventArgs)
        ' 禁用自动检测
        disableAutoCheck = True

        ' 停止定时器
        If jobIdleTimer IsNot Nothing Then
            jobIdleTimer.Stop()
        End If
        If compIdleTimer IsNot Nothing Then
            compIdleTimer.Stop()
        End If

        Dim compName = txtComputerName.Text.Trim()
        If String.IsNullOrWhiteSpace(compName) Then
            MessageBox.Show("请先输入计算机名！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim cfg = ConfigHelper.LoadConfig()
        If Not (cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password")) Then
            MessageBox.Show("请先在设置中填写域信息！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim domainAdmin = cfg("User")
        Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))
        Dim dcAddr = GetDomainControllerAddress()

        If String.IsNullOrWhiteSpace(dcAddr) Then
            MessageBox.Show("未配置域控地址或域名，请在设置中填写", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' 立即开始检测
        ThreadPool.QueueUserWorkItem(Sub()
                                         Dim exists = ComputerExistsOnDC(dcAddr, compName, domainAdmin, domainPwd)
                                         Me.BeginInvoke(Sub()
                                                            If exists Then
                                                                txtComputerName.BackColor = System.Drawing.Color.LightPink
                                                                Dim dr = MessageBox.Show("计算机名已存在，是否覆盖？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                                                                If dr = DialogResult.Yes Then
                                                                    ' 异步删除域控对象
                                                                    Dim progressForm As New ProgressForm()
                                                                    progressForm.Show()
                                                                    ThreadPool.QueueUserWorkItem(Sub()
                                                                                                     Dim delOk = DeleteComputerOnDC(dcAddr, compName, domainAdmin, domainPwd)
                                                                                                     Me.BeginInvoke(Sub()
                                                                                                                        progressForm.Close()
                                                                                                                        If delOk Then
                                                                                                                            MessageBox.Show("域控上同名计算机对象已删除，可继续操作。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                                                                                            lastCompChecked = String.Empty
                                                                                                                            txtComputerName.BackColor = System.Drawing.SystemColors.Window
                                                                                                                        Else
                                                                                                                            MessageBox.Show("删除域控上同名计算机对象失败，请手动检查或确认权限。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                                                                                            lastCompChecked = String.Empty
                                                                                                                            txtComputerName.BackColor = System.Drawing.Color.LightPink
                                                                                                                        End If
                                                                                                                    End Sub)
                                                                                                 End Sub)
                                                                Else
                                                                    lastCompChecked = String.Empty
                                                                    txtComputerName.Focus()
                                                                    txtComputerName.SelectAll()
                                                                End If
                                                            Else
                                                                txtComputerName.BackColor = System.Drawing.Color.LightGreen
                                                            End If
                                                        End Sub)
                                     End Sub)
    End Sub

    ' 修改 TxtJobNumber_KeyDown：按回车触发按钮点击（避免直接调用事件处理器）
    Private Sub TxtJobNumber_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            Try
                If IsDomainJoined() Then
                    If Me.Controls.ContainsKey("btnChangeName") Then
                        CType(Me.Controls("btnChangeName"), Button).PerformClick()
                    End If
                Else
                    If Me.Controls.ContainsKey("btnJoinDomain") Then
                        CType(Me.Controls("btnJoinDomain"), Button).PerformClick()
                    End If
                End If
            Catch ex As Exception
                ' Ignore
            End Try
        End If
    End Sub

    ' 进入计算机名框视为手动编辑
    Private Sub TxtComputerName_Enter(sender As Object, e As EventArgs)
        manualComputerNameEdit = True
    End Sub

    ' 修改 TxtComputerName_KeyDown：按回车触发按钮点击
    Private Sub TxtComputerName_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            Try
                If IsDomainJoined() Then
                    If Me.Controls.ContainsKey("btnChangeName") Then
                        CType(Me.Controls("btnChangeName"), Button).PerformClick()
                    End If
                Else
                    If Me.Controls.ContainsKey("btnJoinDomain") Then
                        CType(Me.Controls("btnJoinDomain"), Button).PerformClick()
                    End If
                End If
            Catch ex As Exception
                ' Ignore
            End Try
        End If
    End Sub

    ' 加域主流程（按钮：加域）
    Private Sub BtnJoinDomain_Click(sender As Object, e As EventArgs) Handles btnJoinDomain.Click
        ' 禁用自动检测，防止在加域过程中触发
        disableAutoCheck = True

        ' 停止所有定时器
        If jobIdleTimer IsNot Nothing Then
            jobIdleTimer.Stop()
        End If
        If compIdleTimer IsNot Nothing Then
            compIdleTimer.Stop()
        End If

        Dim jobNumber = txtJobNumber.Text.Trim()
        Dim newComputerName = txtComputerName.Text.Trim()

        If String.IsNullOrWhiteSpace(jobNumber) OrElse String.IsNullOrWhiteSpace(newComputerName) Then
            MessageBox.Show("请填写工号和计算机名！")
            Return
        End If

        Dim dcAddr = GetDomainControllerAddress()
        If String.IsNullOrWhiteSpace(dcAddr) Then
            MessageBox.Show("未配置域控地址或域名，请在设置中填写""域控地址（DomainController）""或""域名（Domain）"".", "未配置域控", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not PingHost(dcAddr, 1000) Then
            MessageBox.Show("无法连接到域控 " & dcAddr & "，请检查网络。")
            Return
        End If

        If IsDomainJoined() Then
            MessageBox.Show("电脑已加域。")
            Return
        End If

        Dim cfg = ConfigHelper.LoadConfig()
        If Not (cfg.ContainsKey("Domain") AndAlso cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password")) Then
            MessageBox.Show("请先在设置中填写域信息！")
            Return
        End If
        Dim domain = cfg("Domain")
        Dim domainAdmin = cfg("User")
        Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))

        If Not UserExistsOnDC(dcAddr, jobNumber, domainAdmin, domainPwd) Then
            MessageBox.Show("域控上未找到工号 " & jobNumber & "，请确认域账号是否存在。")
            Return
        End If

        Dim progressForm As New ProgressForm()
        progressForm.Show()

        ' 新增：如果目标计算机名与当前机器名相同，则跳过对域控上同名对象的检测与删除
        Dim isSameAsCurrent As Boolean = String.Equals(newComputerName, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
        If isSameAsCurrent Then
        Else
            If ComputerExistsOnDC(dcAddr, newComputerName, domainAdmin, domainPwd) Then
                Dim dr = MessageBox.Show("域控上已存在计算机名 " & newComputerName & "。是否覆盖域控上的对象并继续？", "计算机名已存在", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If dr = DialogResult.No Then
                    progressForm.Close()
                    Return
                End If

                progressForm.SetProgress("正在删除域控上的旧计算机对象...")
                If Not DeleteComputerOnDC(dcAddr, newComputerName, domainAdmin, domainPwd) Then
                    progressForm.Close()
                    MessageBox.Show("删除域控上旧计算机对象失败，请检查权限或手动删除后重试。")
                    Return
                End If
            End If
        End If

        progressForm.SetProgress("正在加入域...")
        Application.DoEvents()
        Thread.Sleep(500)

        Dim ouParam As String = Nothing
        Dim joinResult As Integer = DomainTool.JoinDomain(domain, domainAdmin, domainPwd, ouParam)
        If joinResult <> 0 Then
            progressForm.Close()
            MessageBox.Show("加入域失败，错误信息：" & NetApiErrorMessage(joinResult), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        progressForm.SetProgress("正在确保域控上不存在目标计算机对象...")
        Application.DoEvents()

        Dim deleteSucceeded As Boolean = False
        Dim maxAttempts As Integer = 6
        Dim attempt As Integer = 0

        ' 若与当前计算机名相同，跳过检测/删除循环
        If isSameAsCurrent Then
            deleteSucceeded = True
        Else
            While Not deleteSucceeded AndAlso attempt < maxAttempts
                attempt += 1
                If Not ComputerExistsOnDC(dcAddr, newComputerName, domainAdmin, domainPwd) Then
                    deleteSucceeded = True
                    Exit While
                End If

                If DeleteComputerOnDC(dcAddr, newComputerName, domainAdmin, domainPwd) Then
                    Thread.Sleep(800)
                    If Not ComputerExistsOnDC(dcAddr, newComputerName, domainAdmin, domainPwd) Then
                        deleteSucceeded = True
                        Exit While
                    End If
                End If

                Thread.Sleep(1000)
            End While
        End If

        If Not deleteSucceeded Then
            progressForm.Close()
            MessageBox.Show("无法删除域控上目标计算机对象（可能被占用或权限不足），重命名可能失败。请手动删除域控上的该计算机对象后重试。")
            Return
        End If

        progressForm.SetProgress("正在修改计算机名...")
        Application.DoEvents()
        Thread.Sleep(500)

        If Not ChangeComputerNameByPowerShell(newComputerName, domainAdmin, domainPwd) Then
            progressForm.Close()
            MessageBox.Show("PowerShell修改计算机名失败，请检查账号权限！")
            Return
        End If

        progressForm.SetProgress("正在添加域用户到本地管理员组...")
        Application.DoEvents()
        Thread.Sleep(500)

        If Not AddDomainUserToLocalAdmin(jobNumber) Then
            progressForm.Close()
            MessageBox.Show("添加域用户到本地管理员组失败或被设置禁止，请确认工号和域名正确或设置项。")
            Return
        End If

        progressForm.SetProgress("正在设置登录界面显示为域账号...")
        Application.DoEvents()
        Thread.Sleep(500)

        SetLogonUIAccount(domain, jobNumber)

        progressForm.SetProgress("正在写入资产编号...")
        Application.DoEvents()
        Thread.Sleep(300)
        SetRegisteredOwner(GetAssetNumber())

        progressForm.SetProgress("全部操作完成！")
        Application.DoEvents()
        Thread.Sleep(500)
        progressForm.Close()

        ' 清除检测缓存，避免后续自动检测误报
        lastJobChecked = String.Empty
        lastCompChecked = String.Empty

        UpdateStatusBall("joined")

        ' 使用 30 秒倒计时重启（除非用户取消）
        ShowRestartCountdown(30)
    End Sub

    ' 更换域账号流程（原样保留）
    Private Sub BtnChangeName_Click(sender As Object, e As EventArgs) Handles btnChangeName.Click
        ' 禁用自动检测，防止在更换域账号过程中触发
        disableAutoCheck = True

        ' 停止所有定时器
        If jobIdleTimer IsNot Nothing Then
            jobIdleTimer.Stop()
        End If
        If compIdleTimer IsNot Nothing Then
            compIdleTimer.Stop()
        End If

        Try
            Me.btnChangeName.Enabled = False
            Me.btnJoinDomain.Enabled = False
            Application.DoEvents()

            Dim cfg = ConfigHelper.LoadConfig()
            If Not (cfg.ContainsKey("Domain") AndAlso cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password")) Then
                MessageBox.Show("请先在设置中填写域信息！")
                Return
            End If
            Dim domain = cfg("Domain")
            Dim domainAdmin = cfg("User")
            Dim domainPwd = ConfigHelper.Decrypt(cfg("Password"))

            Dim dcAddr = GetDomainControllerAddress()
            If String.IsNullOrWhiteSpace(dcAddr) Then
                MessageBox.Show("未配置域控地址或域名，请在设置中填写""域控地址（DomainController）""或""域名（Domain）"".", "未配置域控", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not PingHost(dcAddr, 1000) Then
                MessageBox.Show("无法连接到域控 " & dcAddr & "，请检查网络。")
                Return
            End If

            Dim jobNumber = txtJobNumber.Text.Trim()
            Dim newName = txtComputerName.Text.Trim()
            If String.IsNullOrWhiteSpace(jobNumber) OrElse String.IsNullOrWhiteSpace(newName) Then
                MessageBox.Show("请填写工号和计算机名！")
                Return
            End If

            If Not UserExistsOnDC(dcAddr, jobNumber, domainAdmin, domainPwd) Then
                MessageBox.Show("域控上未找到工号 " & jobNumber & "，请确认域账号是否存在。")
                Return
            End If

            ' 下面为原有更换域账号逻辑（保持不变）
            Dim progressForm As New ProgressForm()
            progressForm.Show()
            progressForm.SetProgress("正在更换域账号（修改计算机名）...")
            Application.DoEvents()
            Thread.Sleep(300)

            progressForm.SetProgress("确保域控上不存在目标计算机对象...")
            Application.DoEvents()

            ' 新增：如目标名与当前机器名相同，则跳过域控对象检测/删除
            Dim isSameAsCurrentChange As Boolean = String.Equals(newName, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            Dim deleteOk As Boolean = False
            Dim attempts As Integer = 0
            Dim maxAttempts As Integer = 6

            If isSameAsCurrentChange Then
                deleteOk = True
            Else
                While Not deleteOk AndAlso attempts < maxAttempts
                    attempts += 1
                    If Not ComputerExistsOnDC(dcAddr, newName, domainAdmin, domainPwd) Then
                        deleteOk = True
                        Exit While
                    End If

                    If DeleteComputerOnDC(dcAddr, newName, domainAdmin, domainPwd) Then
                        Thread.Sleep(800)
                        If Not ComputerExistsOnDC(dcAddr, newName, domainAdmin, domainPwd) Then
                            deleteOk = True
                            Exit While
                        End If
                    End If

                    Thread.Sleep(500)
                End While
            End If

            If Not deleteOk Then
                progressForm.Close()
                Dim dr = MessageBox.Show("无法删除域控上目标计算机对象，是否继续尝试修改计算机名？(若继续可能失败)", "无法删除域对象", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If dr = DialogResult.No Then
                    Return
                End If
                progressForm = New ProgressForm()
                progressForm.Show()
                progressForm.SetProgress("继续执行更换计算机名...")
                Application.DoEvents()
                Thread.Sleep(200)
            End If

            If Not ChangeComputerNameByPowerShell(newName, domainAdmin, domainPwd) Then
                progressForm.Close()
                MessageBox.Show("PowerShell修改计算机名失败，请检查账号权限或域对象状态！")
                Return
            End If

            progressForm.SetProgress("正在添加域用户到本地管理员组...")
            Application.DoEvents()
            Thread.Sleep(300)

            If Not LocalAdminHasDomainUser(jobNumber, domain) Then
                If Not AddDomainUserToLocalAdmin(jobNumber) Then
                    progressForm.Close()
                    MessageBox.Show("添加域用户到本地管理员组失败或被设置禁止，请确认工号和域名正确或设置项！")
                    Return
                End If
            End If

            progressForm.SetProgress("正在设置登录界面显示为域账号...")
            Application.DoEvents()
            Thread.Sleep(300)

            SetLogonUIAccount(domain, jobNumber)

            progressForm.SetProgress("正在写入资产编号...")
            Application.DoEvents()
            Thread.Sleep(300)
            SetRegisteredOwner(GetAssetNumber())

            progressForm.SetProgress("操作完成，准备重启...")
            Application.DoEvents()
            Thread.Sleep(300)
            progressForm.Close()

            ' 清除检测缓存，避免后续自动检测误报
            lastJobChecked = String.Empty
            lastCompChecked = String.Empty

            ' 刷新界面加域状态（保持为已加域）
            UpdateStatusBall("joined")

            ShowRestartCountdown(30)
        Catch ex As Exception
            MessageBox.Show("更换域账号发生异常：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Me.btnChangeName.Enabled = True
            Me.btnJoinDomain.Enabled = True
        End Try
    End Sub

    ' 添加域用户到本地管理员组（带检测，若已存在则跳过并返回 True）
    ' 支持配置项 "AddDomainToLocalAdmin" = "true"/"false"（不指定默认为 true）
    Private Function AddDomainUserToLocalAdmin(jobNumber As String) As Boolean
        Try
            Dim cfg = ConfigHelper.LoadConfig()
            If Not cfg.ContainsKey("Domain") Then
                Return False
            End If
            Dim domain = cfg("Domain")

            ' 检查设置是否允许添加域用户到本地管理员组（设置中复选框控制）
            Dim allowAdd As Boolean = True
            Try
                If cfg.ContainsKey("AddDomainToLocalAdmin") Then
                    Dim v = cfg("AddDomainToLocalAdmin").ToString().Trim().ToLowerInvariant()
                    If v = "false" OrElse v = "0" OrElse v = "no" Then
                        allowAdd = False
                    End If
                End If
            Catch ex As Exception
                ' Use default
                allowAdd = True
            End Try

            If Not allowAdd Then
                Return True ' 视为成功但未执行添加
            End If

            ' 先检查是否已经存在，若存在则直接返回 True（不报错）
            If LocalAdminHasDomainUser(jobNumber, domain) Then
                Return True
            End If

            Dim computerName = Environment.MachineName
            Dim group As New DirectoryEntry($"WinNT://{computerName}/Administrators,group")

            ' 尝试添加
            group.Invoke("Add", New Object() {"WinNT://" & domain & "/" & jobNumber})

            ' 添加成功后：移除当前登录的域用户（如果是域用户），本操作仅从本地 Administrators 组中移除当前登录用户
            Try
                RemoveCurrentDomainUserFromLocalAdmins()
            Catch ex As Exception
                ' Ignore
            End Try

            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    ' 使用 PowerShell 的 -DomainCredential 修改计算机名（不自动重启）
    Private Function ChangeComputerNameByPowerShell(newName As String, domainAdmin As String, domainPwd As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(newName) OrElse String.IsNullOrWhiteSpace(domainAdmin) Then
                MessageBox.Show("修改计算机名失败：域管理员账号或目标计算机名为空。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return False
            End If

            Dim pwdEsc = domainPwd.Replace("'", "''")
            Dim adminEsc = domainAdmin.Replace("'", "''")
            Dim nameEsc = newName.Replace("'", "''")

            Dim psCmd As String =
                "$secpasswd = ConvertTo-SecureString '" & pwdEsc & "' -AsPlainText -Force;" &
                "$cred = New-Object PSCredential('" & adminEsc & "', $secpasswd);" &
                "Rename-Computer -NewName '" & nameEsc & "' -DomainCredential $cred -Force -ErrorAction Stop;" &
                "Write-Output 'RENAME_OK'"

            ' 使用单独变量构造完整参数，避免行内复杂嵌套引号导致解析问题
            Dim arg As String = "-NoProfile -ExecutionPolicy Bypass -Command " & Chr(34) & psCmd & Chr(34)

            Dim psi As New ProcessStartInfo("powershell.exe", arg) With {
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True
            }

            Using proc As Process = Process.Start(psi)
                Dim output As String = proc.StandardOutput.ReadToEnd()
                Dim err As String = proc.StandardError.ReadToEnd()
                proc.WaitForExit()

                If proc.ExitCode = 0 AndAlso output.Contains("RENAME_OK") Then
                    Return True
                Else
                    Dim msg As String = "修改计算机名失败。"
                    If Not String.IsNullOrWhiteSpace(err) Then
                        msg &= vbCrLf & "错误信息: " & err
                    ElseIf Not String.IsNullOrWhiteSpace(output) Then
                        msg &= vbCrLf & "输出: " & output
                    End If
                    MessageBox.Show(msg, "修改计算机名失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return False
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show("修改计算机名发生异常：" & ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    ' 设置登录界面显示为域账号（强制 64 位视图）
    Private Sub SetLogonUIAccount(domain As String, userName As String)
        Try
            Dim samUser = domain & "\" & userName

            ' 删除 DefaultUserName（64 位视图）
            Using winlogonKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", writable:=True)
                If winlogonKey IsNot Nothing Then
                    Try
                        winlogonKey.DeleteValue("DefaultUserName", False)
                    Catch ex As Exception
                        ' Ignore
                    End Try
                End If
            End Using

            ' 设置 LogonUI 下的域账号显示（64 位视图）
            Using logonUIKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI", writable:=True)
                If logonUIKey IsNot Nothing Then
                    logonUIKey.SetValue("LastLoggedOnDisplayName", samUser, RegistryValueKind.String)
                    logonUIKey.SetValue("LastLoggedOnSAMUser", samUser, RegistryValueKind.String)
                    logonUIKey.SetValue("LastLoggedOnUser", samUser, RegistryValueKind.String)
                Else
                    MessageBox.Show("无法打开 LogonUI 注册表项，请以管理员身份运行！")
                End If
            End Using
        Catch ex As Exception
            ' Ignore
        End Try
    End Sub

    Private Sub BtnSettings_Click(sender As Object, e As EventArgs) Handles btnSettings.Click
        Dim settingsForm As New SettingsForm()
        settingsForm.ShowDialog()
    End Sub

    Private clickCount As Integer = 0
    Private lastClickTime As DateTime = DateTime.MinValue

    Private Sub LblSecret_Click(sender As Object, e As EventArgs) Handles lblSecret.Click
        ' 三次快速点击（2秒内）显示隐藏选项
        If (DateTime.Now - lastClickTime).TotalMilliseconds > 2000 Then
            clickCount = 0
        End If
        clickCount += 1
        lastClickTime = DateTime.Now

        If clickCount >= 3 Then
            clickCount = 0
            If Me.Controls.ContainsKey("btnChangeName") Then
                CType(Me.Controls("btnChangeName"), Button).Visible = True
            End If
            If Me.Controls.ContainsKey("btnUnjoinDomain") Then
                CType(Me.Controls("btnUnjoinDomain"), Button).Visible = True
            End If
        End If
    End Sub

    Private Sub BtnUnjoinDomain_Click(sender As Object, e As EventArgs) Handles btnUnjoinDomain.Click
        Dim cfg = ConfigHelper.LoadConfig()
        If cfg.ContainsKey("User") AndAlso cfg.ContainsKey("Password") Then
            Dim user = cfg("User")
            Dim password = ConfigHelper.Decrypt(cfg("Password"))
            Dim dcAddr = GetDomainControllerAddress()
            If String.IsNullOrWhiteSpace(dcAddr) Then
                MessageBox.Show("未配置域控地址或域名，请在设置中填写""域控地址（DomainController）""或""域名（Domain）"".", "未配置域控", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not PingHost(dcAddr, 1000) Then
                MessageBox.Show("无法访问域控，退出域操作可能失败。请检查网络/DNS/域控后重试。", "域控不可达", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim result As Integer = DomainTool.UnjoinDomain(user, password)
            If result = 0 Then
                MessageBox.Show("成功退出域，请重启电脑。")
                ' 刷新加域状态显示
                UpdateStatusBall("notjoined")
            Else
                MessageBox.Show("退出域失败，错误信息：" & NetApiErrorMessage(result), "退出域失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Else
            MessageBox.Show("请先在设置中填写域信息！")
        End If
    End Sub

    ''' <summary>
    ''' 更新状态球的颜色
    ''' </summary>
    ''' <param name="status">joined=已加域, notjoined=未加域, unknown=未知, unverified=未验证, error=异常</param>
    Private Sub UpdateStatusBall(status As String)
        If Not Me.Controls.ContainsKey("picStatusBall") Then Return
        Dim ball = CType(Me.Controls("picStatusBall"), PictureBox)
        Dim ballColor As Color
        Dim tipText As String
        Select Case status.ToLower()
            Case "joined"
                ballColor = Color.Green
                tipText = "已加域"
            Case "notjoined"
                ballColor = Color.Gray
                tipText = "未加域"
            Case "unverified"
                ballColor = Color.Orange
                tipText = "已加域（未验证）"
            Case "error"
                ballColor = Color.Red
                tipText = "异常"
            Case Else
                ballColor = Color.Gray
                tipText = "未知"
        End Select
        DrawCircleBall(ball, ballColor)
        ToolTip1.SetToolTip(ball, tipText)
    End Sub

    ''' <summary>
    ''' 绘制圆形状态球
    ''' </summary>
    Private Sub DrawCircleBall(pb As PictureBox, fillColor As Color)
        ' 先释放旧的 Image
        If pb.Image IsNot Nothing Then
            pb.Image.Dispose()
            pb.Image = Nothing
        End If

        Dim bmp As New Bitmap(pb.Width, pb.Height)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            g.Clear(Color.Transparent)
            Using brush As New SolidBrush(fillColor)
                g.FillEllipse(brush, 0, 0, pb.Width - 1, pb.Height - 1)
            End Using
        End Using
        pb.Image = bmp
    End Sub

End Class