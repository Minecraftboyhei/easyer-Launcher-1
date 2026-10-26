Imports System.IO
Imports System.Text.Json

Namespace Config

    ''' <summary>启动器配置。</summary>
    Public Class LauncherConfig

        Public Property GameDirectory As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".easyer")

        Public Property JavaPath As String = "java"
        Public Property MaxMemoryMB As Integer = 4096
        Public Property WindowWidth As Integer = 1280
        Public Property WindowHeight As Integer = 720
        Public Property FullScreen As Boolean = False

        ' ==== 主题 ====
        Public Property ThemeMode As String = "dark"          ' dark / light / custom
        Public Property AccentColor As String = "#60A5FA"     ' Win11 蓝风格默认强调色
        Public Property CustomBackground As String = "#1B1B1F"
        Public Property CustomCard As String = "#2A2A30"
        Public Property CustomForeground As String = "#F2F2F7"

        ' ==== 账号 ====
        Public Property Accounts As New List(Of AccountInfo)
        Public Property ActiveAccountName As String = ""

        ' ==== 正版登录（需自建 Azure 应用后填入 ClientId）====
        Public Property MsClientId As String = ""

        Public Shared Function DefaultPath() As String
            Return Path.Combine(AppContext.BaseDirectory, "easyer-config.json")
        End Function

        Public Shared Function Load() As LauncherConfig
            Try
                Dim p = DefaultPath()
                If File.Exists(p) Then
                    Return JsonSerializer.Deserialize(Of LauncherConfig)(File.ReadAllText(p))
                End If
            Catch
            End Try
            Return New LauncherConfig()
        End Function

        Public Sub Save()
            Dim opts As New JsonSerializerOptions With {.WriteIndented = True, .Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}
            File.WriteAllText(DefaultPath(), JsonSerializer.Serialize(Me, opts))
        End Sub

        Public Function GetActiveAccount() As AccountInfo
            For Each a In Accounts
                If a.Name.Equals(ActiveAccountName, StringComparison.OrdinalIgnoreCase) Then Return a
            Next
            If Accounts.Count > 0 Then Return Accounts(0)
            Return Nothing
        End Function

    End Class

    ''' <summary>账号信息（正版或离线）。</summary>
    Public Class AccountInfo
        Public Property Name As String = ""
        Public Property Uuid As String = ""
        Public Property AccessToken As String = ""      ' 离线为 "0"/占位；正版为 MC token
        Public Property AccountType As String = "offline"  ' offline / microsoft
        Public Property MsRefreshToken As String = ""   ' 正版刷新令牌
        Public Property MsExpiresAt As String = ""      ' token 过期时间（本地）
        Public Property AuthServer As String = ""       ' 外置登录的认证服务器名（littleskin 等）
    End Class

End Namespace
