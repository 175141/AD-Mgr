Imports System.Runtime.InteropServices

Public Class DomainTool

    ' SetComputerNameEx
    <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
    Public Shared Function SetComputerNameEx(
        ByVal NameType As Integer,
        ByVal lpBuffer As String
    ) As Boolean
    End Function

    ' NetJoinDomain
    <DllImport("Netapi32.dll", CharSet:=CharSet.Unicode)>
    Public Shared Function NetJoinDomain(
        ByVal lpServer As String,
        ByVal lpDomain As String,
        ByVal lpAccountOU As String,
        ByVal lpAccount As String,
        ByVal lpPassword As String,
        ByVal fJoinOptions As Integer
    ) As Integer
    End Function

    ' NetUnjoinDomain
    <DllImport("Netapi32.dll", CharSet:=CharSet.Unicode)>
    Public Shared Function NetUnjoinDomain(
        ByVal lpServer As String,
        ByVal lpAccount As String,
        ByVal lpPassword As String,
        ByVal fUnjoinOptions As Integer
    ) As Integer
    End Function

    ' 常量定义（保留常见 join 标志）
    Public Const ComputerNamePhysicalDnsHostname As Integer = 5
    Public Const NETSETUP_JOIN_DOMAIN As Integer = &H1
    Public Const NETSETUP_ACCT_CREATE As Integer = &H2
    Public Const NETSETUP_DOMAIN_JOIN_IF_JOINED As Integer = &H20
    ' 退域默认使用 0（无标志）。若需要删除域控上的计算机帐号，可考虑使用 NETSETUP_ACCT_DELETE = &H4
    Public Const NETSETUP_UNJOIN_DEFAULT As Integer = 0
    Public Const NETSETUP_ACCT_DELETE As Integer = &H4

    ' 修改计算机名
    Public Shared Function ChangeComputerName(newName As String) As Boolean
        Return SetComputerNameEx(ComputerNamePhysicalDnsHostname, newName)
    End Function

    ' 加入域
    Public Shared Function JoinDomain(domain As String, username As String, password As String, Optional ou As String = Nothing) As Integer
        Return NetJoinDomain(
            Nothing,
            domain,
            ou,
            username,
            password,
            NETSETUP_JOIN_DOMAIN Or NETSETUP_ACCT_CREATE Or NETSETUP_DOMAIN_JOIN_IF_JOINED
        )
    End Function

    ' 退出域：使用 0（默认无标志）。如果你需要退域时同时删除域控上计算机对象，可改为传 NETSETUP_ACCT_DELETE
    Public Shared Function UnjoinDomain(username As String, password As String) As Integer
        Return NetUnjoinDomain(
            Nothing,
            username,
            password,
            NETSETUP_UNJOIN_DEFAULT
        )
    End Function

End Class
