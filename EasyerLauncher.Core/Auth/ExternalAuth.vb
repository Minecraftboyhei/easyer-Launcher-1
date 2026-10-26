Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Auth

    ''' <summary>
    ''' 外置登录（authlib-injector / LittleSkin）：微软正版之外的免费登录方式，
    ''' 不需要 Azure、不需要任何卡，littleskin.cn 注册个账号即可。
    ''' </summary>
    Public Class ExternalAuth

        ''' <summary>LittleSkin 的 Yggdrasil API 根地址（注意：老地址 authserver.littleskin.cn 已废弃）。</summary>
        Public Const AuthRoot As String = "https://littleskin.cn/api/yggdrasil"

        Public Class ExternalProfile
            Public Property Name As String
            Public Property Uuid As String
            Public Property AccessToken As String
        End Class

        ''' <summary>用户名密码登录（username 填邮箱或角色名）。</summary>
        Public Shared Function Login(username As String, password As String) As ExternalProfile
            Dim payload As New Dictionary(Of String, Object) From {
                {"agent", New Dictionary(Of String, Object) From {
                    {"name", "Minecraft"}, {"version", 1}}},
                {"username", username},
                {"password", password},
                {"requestUser", True}
            }
            Dim body = JsonSerializer.Serialize(payload)
            Dim resp = Http.PostJson(AuthRoot & "/authserver/authenticate", body)
            Using doc = JsonDocument.Parse(resp)
                Dim r = doc.RootElement
                Dim errEl As JsonElement
                If r.TryGetProperty("error", errEl) Then
                    Dim msg = ""
                    Dim mEl As JsonElement
                    If r.TryGetProperty("errorMessage", mEl) Then msg = mEl.GetString()
                    Throw New Exception("外置登录失败：" & If(msg <> "", msg, errEl.GetString()))
                End If
                If Not r.TryGetProperty("selectedProfile", errEl) Then
                    Throw New Exception("这个 LittleSkin 账号还没有角色。" & Environment.NewLine &
                                        "请先到 littleskin.cn 登录并创建一个角色名，再回来登录。")
                End If
                Dim prof = r.GetProperty("selectedProfile")
                Return New ExternalProfile With {
                    .Name = prof.GetProperty("name").GetString(),
                    .Uuid = prof.GetProperty("id").GetString(),
                    .AccessToken = r.GetProperty("accessToken").GetString()
                }
            End Using
        End Function

        ''' <summary>确保 authlib-injector.jar 已下载（首次自动拉最新版），返回 jar 路径。</summary>
        Public Shared Function EnsureInjectorJar(gameDir As String) As String
            Dim dir = Path.Combine(gameDir, "authlib-injector")
            Directory.CreateDirectory(dir)
            Dim dest = Path.Combine(dir, "authlib-injector.jar")
            If File.Exists(dest) AndAlso New FileInfo(dest).Length > 100000 Then Return dest

            Dim latest = Http.GetStringAuto("https://authlib-injector.yushi.moe/artifact/latest.json")
            Using doc = JsonDocument.Parse(latest)
                Dim url = doc.RootElement.GetProperty("download_url").GetString()
                Http.DownloadFileAuto(url, dest)
            End Using
            Return dest
        End Function

    End Class

End Namespace
