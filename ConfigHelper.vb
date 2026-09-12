Imports System.IO
Imports System.Text

Public Class ConfigHelper
    Public Shared ReadOnly ConfigPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini")

    ' 简单保存接口（覆盖）
    Public Shared Sub SaveConfig(domain As String, user As String, password As String, ou As String, Optional domainController As String = "", Optional addDomainToLocalAdmin As Boolean? = Nothing)
        Dim dict As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        dict("Domain") = If(domain, String.Empty)
        dict("User") = If(user, String.Empty)
        dict("Password") = Encrypt(If(password, String.Empty))
        dict("OU") = If(ou, String.Empty)

        If Not String.IsNullOrWhiteSpace(domainController) Then
            dict("DomainController") = domainController.Trim()
        End If

        If addDomainToLocalAdmin.HasValue Then
            dict("AddDomainToLocalAdmin") = If(addDomainToLocalAdmin.Value, "true", "false")
        End If

        SaveConfig(dict)
    End Sub

    ' 将整个字典写回配置文件（覆盖）
    Public Shared Sub SaveConfig(values As Dictionary(Of String, String))
        Dim lines As New List(Of String)()
        ' 保证写入顺序（可根据需要调整）
        Dim keysOrder As String() = {"Domain", "DomainController", "User", "Password", "OU", "AddDomainToLocalAdmin"}
        For Each k In keysOrder
            If values.ContainsKey(k) Then
                lines.Add(k & "=" & values(k))
            End If
        Next
        ' 写入其余键（若有）
        For Each kv In values
            If Not keysOrder.Contains(kv.Key) Then
                lines.Add(kv.Key & "=" & kv.Value)
            End If
        Next

        Dim dir = Path.GetDirectoryName(ConfigPath)
        If String.IsNullOrWhiteSpace(dir) Then
            dir = AppDomain.CurrentDomain.BaseDirectory
        End If
        Directory.CreateDirectory(dir)
        File.WriteAllLines(ConfigPath, lines, Encoding.UTF8)
    End Sub

    ' 读取配置到字典。支持注释（以 # 或 ; 开头）并忽略空行。
    Public Shared Function LoadConfig() As Dictionary(Of String, String)
        Dim dict As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        If Not File.Exists(ConfigPath) Then
            Return dict
        End If

        For Each rawLine In File.ReadAllLines(ConfigPath, Encoding.UTF8)
            Dim line = rawLine.Trim()
            If String.IsNullOrWhiteSpace(line) Then Continue For
            If line.StartsWith("#") OrElse line.StartsWith(";") Then Continue For

            Dim idx = line.IndexOf("="c)
            If idx <= 0 Then Continue For
            Dim key = line.Substring(0, idx).Trim()
            Dim value = line.Substring(idx + 1).Trim()
            dict(key) = value
        Next
        Return dict
    End Function

    Public Shared Function Encrypt(str As String) As String
        If str Is Nothing Then str = String.Empty
        Dim bytes = Encoding.UTF8.GetBytes(str)
        Return Convert.ToBase64String(bytes)
    End Function

    Public Shared Function Decrypt(str As String) As String
        If String.IsNullOrWhiteSpace(str) Then Return String.Empty
        Try
            Dim bytes = Convert.FromBase64String(str)
            Return Encoding.UTF8.GetString(bytes)
        Catch
            ' 如果不是 Base64（兼容旧数据/手动修改），直接返回原串
            Return str
        End Try
    End Function
End Class
