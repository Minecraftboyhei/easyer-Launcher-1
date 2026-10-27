Imports System.IO
Imports System.Linq
Imports System.Text.Json

Namespace Config

    ''' <summary>启动器配置。</summary>
    Public Class LauncherConfig

        ''' <summary>游戏目录。留空 = 走便携逻辑（exe 同级的 .minecraft），启动时由 EnsureGameDirectory 决定。</summary>
        Public Property GameDirectory As String = ""

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

        ''' <summary>便携游戏目录：exe 所在的同一目录下的 .minecraft。</summary>
        Public Shared Function PortableGameDir() As String
            Return Path.Combine(AppContext.BaseDirectory, ".minecraft")
        End Function

        ''' <summary>旧版默认目录（%APPDATA%\.easyer），仅用于迁移与回退。</summary>
        Public Shared Function LegacyGameDir() As String
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".easyer")
        End Function

        ''' <summary>
        ''' 决定本次运行使用的游戏目录，并确保它存在。
        ''' 默认 = exe 同级的 .minecraft（便携，整个文件夹可以带着走）；
        ''' exe 所在位置不可写（如 Program Files）时回退到 %APPDATA%\.easyer；
        ''' 老用户数据在旧目录时自动搬过来——同盘直接重命名（瞬间完成），
        ''' 跨盘则建一个目录联接（junction），不复制几十 G 的游戏文件。
        ''' </summary>
        Public Shared Function EnsureGameDirectory(cfg As LauncherConfig) As String
            Dim portableDir = PortableGameDir()
            Dim legacyDir = LegacyGameDir()

            ' ---- exe 目录可写吗 ----
            Dim portableOk As Boolean = False
            Try
                Directory.CreateDirectory(portableDir)
                Dim probe = Path.Combine(portableDir, ".write-test")
                File.WriteAllText(probe, "ok")
                File.Delete(probe)
                portableOk = True
            Catch
                portableOk = False
            End Try

            If Not portableOk Then
                Try
                    Directory.CreateDirectory(legacyDir)
                Catch
                End Try
                Return legacyDir
            End If

            ' ---- 用户自己指定过别的目录，尊重 ----
            Dim current = If(cfg Is Nothing, "", If(cfg.GameDirectory, "").Trim())
            If current.Length > 0 AndAlso Not SamePath(current, legacyDir) AndAlso Not SamePath(current, portableDir) Then
                Try
                    Directory.CreateDirectory(current)
                Catch
                End Try
                Return current
            End If

            ' ---- 旧数据 >= 便携目录为空 → 迁移 ----
            If DirHasContent(legacyDir) AndAlso Not DirHasContent(portableDir) Then
                Try
                    Directory.Delete(portableDir, True)
                    Directory.Move(legacyDir, portableDir)          ' 同盘：仅重命名，瞬间完成
                    Try
                        Directory.CreateDirectory(portableDir)
                    Catch
                    End Try
                    Return portableDir
                Catch
                    ' 跨盘：建目录联接（不复制文件，不占额外空间）
                    Try
                        Try
                            Directory.Delete(portableDir, True)
                        Catch
                        End Try
                        Dim psi As New ProcessStartInfo("cmd.exe",
                            "/c mklink /J """ & portableDir & """ """ & legacyDir & """") With {
                            .CreateNoWindow = True, .UseShellExecute = False}
                        Using p = Process.Start(psi)
                            p.WaitForExit(5000)
                        End Using
                        If Directory.Exists(portableDir) Then Return portableDir
                    Catch
                    End Try
                    ' 两条路都失败：留在旧目录，绝不让已有游戏"消失"
                    Return legacyDir
                End Try
            End If

            Return portableDir
        End Function

        Private Shared Function DirHasContent(path As String) As Boolean
            Try
                Return Directory.Exists(path) AndAlso Directory.EnumerateFileSystemEntries(path).Any()
            Catch
                Return False
            End Try
        End Function

        Private Shared Function SamePath(a As String, b As String) As Boolean
            Try
                Return String.Equals(Path.GetFullPath(a).TrimEnd("\"c), Path.GetFullPath(b).TrimEnd("\"c),
                                     StringComparison.OrdinalIgnoreCase)
            Catch
                Return False
            End Try
        End Function

        Public Shared Function Load() As LauncherConfig
            Dim cfg As LauncherConfig = Nothing
            Try
                Dim p = DefaultPath()
                If File.Exists(p) Then
                    cfg = JsonSerializer.Deserialize(Of LauncherConfig)(File.ReadAllText(p))
                End If
            Catch
            End Try
            If cfg Is Nothing Then cfg = New LauncherConfig()
            ' 空值或乱填（不是 GUID）都回落到内置 Client ID，保证任何人拿到就是开箱可用
            Dim id As Guid
            If String.IsNullOrWhiteSpace(cfg.MsClientId) OrElse Not Guid.TryParse(cfg.MsClientId.Trim(), id) Then
                cfg.MsClientId = EasyerLauncher.Core.Auth.MicrosoftAuth.DefaultClientId
            End If
            Return cfg
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
