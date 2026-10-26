Imports System.Collections.Generic
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Auth

    ''' <summary>离线账号（authlib 离线 UUID 算法）。</summary>
    Public Class OfflineAuth

        ''' <summary>等价于 Java UUID.nameUUIDFromBytes("OfflinePlayer:" + name)。</summary>
        Public Shared Function GetOfflineUuid(name As String) As String
            Dim data = Encoding.UTF8.GetBytes("OfflinePlayer:" & name)
            Dim md5Prov As MD5 = MD5.Create()
            Dim hash = md5Prov.ComputeHash(data)
            hash(6) = CByte((hash(6) And &HF) Or &H30)  ' version 3
            hash(8) = CByte((hash(8) And &H3F) Or &H80) ' variant IETF
            Dim hex = BitConverter.ToString(hash).Replace("-", "").ToLower()
            Return String.Format("{0}-{1}-{2}-{3}-{4}",
                hex.Substring(0, 8), hex.Substring(8, 4), hex.Substring(12, 4),
                hex.Substring(16, 4), hex.Substring(20, 12))
        End Function

        Public Shared Function Login(name As String) As Config.AccountInfo
            Return New Config.AccountInfo With {
                .Name = name,
                .Uuid = GetOfflineUuid(name),
                .AccessToken = "0",
                .AccountType = "offline"
            }
        End Function

    End Class

    ''' <summary>Microsoft 正版登录：设备码流程 → XBL → XSTS → Minecraft Services。</summary>
    Public Class MicrosoftAuth

        Private Const Tenant As String = "consumers"
        Private Const Scope As String = "XboxLive.signin offline_access"
        Private Const TokenUrl As String = "https://login.microsoftonline.com/" & Tenant & "/oauth2/v2.0/token"
        Private Const DeviceCodeUrl As String = "https://login.microsoftonline.com/" & Tenant & "/oauth2/v2.0/devicecode"

        Public Class DeviceCodeResult
            Public Property UserCode As String
            Public Property VerificationUri As String
            Public Property DeviceCode As String
            Public Property Interval As Integer
            Public Property Message As String
        End Class

        Public Class McProfile
            Public Property Name As String
            Public Property Uuid As String
            Public Property AccessToken As String
            Public Property RefreshToken As String
        End Class

        ''' <summary>第一步：请求设备码。</summary>
        Public Shared Function BeginDeviceCode(clientId As String) As DeviceCodeResult
            Dim form As New Dictionary(Of String, String) From {
                {"client_id", clientId},
                {"scope", Scope}
            }
            Dim resp = Http.PostForm(DeviceCodeUrl, form)
            Using doc = JsonDocument.Parse(resp)
                Dim r = doc.RootElement
                Dim errEl As JsonElement
                If r.TryGetProperty("error", errEl) Then
                    Dim desc = ""
                    Dim descEl As JsonElement
                    If r.TryGetProperty("error_description", descEl) Then desc = descEl.GetString()
                    If desc.Contains("AADSTS700016") Then
                        Throw New Exception("Client ID 无效：这个应用不存在或不是你账号下的。" & Environment.NewLine &
                                            "请到 Azure 门户 → 应用注册，新建一个应用并复制它的「应用程序(客户端) ID」。")
                    End If
                    Throw New Exception("微软登录错误: " & errEl.GetString() & If(desc <> "", " — " & desc, ""))
                End If
                Dim interval As Integer = 5
                If r.TryGetProperty("interval", errEl) Then interval = CInt(errEl.GetInt32())
                Return New DeviceCodeResult With {
                    .UserCode = r.GetProperty("user_code").GetString(),
                    .VerificationUri = r.GetProperty("verification_uri").GetString(),
                    .DeviceCode = r.GetProperty("device_code").GetString(),
                    .Interval = interval,
                    .Message = If(r.TryGetProperty("message", errEl), errEl.GetString(), "")
                }
            End Using
        End Function

        ''' <summary>第二步：轮询令牌 → XBL → MC。返回 MC 档案；未授权时返回 Nothing。</summary>
        Public Shared Function PollAndFinish(clientId As String, dc As DeviceCodeResult) As McProfile
            Dim form As New Dictionary(Of String, String) From {
                {"client_id", clientId},
                {"device_code", dc.DeviceCode},
                {"grant_type", "urn:ietf:params:oauth:grant-type:device_code"}
            }
            Dim resp = Http.PostForm(TokenUrl, form)
            Using doc = JsonDocument.Parse(resp)
                Dim r = doc.RootElement
                Dim errEl As JsonElement
                If r.TryGetProperty("error", errEl) Then
                    Dim err = errEl.GetString()
                    If err = "authorization_pending" OrElse err = "slow_down" Then Return Nothing
                    Dim desc = ""
                    Dim descEl As JsonElement
                    If r.TryGetProperty("error_description", descEl) Then desc = descEl.GetString()
                    Throw New Exception("微软登录错误: " & err & If(desc <> "", " — " & desc, ""))
                End If
                Dim msAccessToken = r.GetProperty("access_token").GetString()
                Dim refreshEl As JsonElement
                Dim refreshToken As String = ""
                If r.TryGetProperty("refresh_token", refreshEl) Then refreshToken = refreshEl.GetString()

                Return XboxToMcProfile(msAccessToken, refreshToken)
            End Using
        End Function

        ''' <summary>用 MS access_token 完成 XBL→XSTS→MC 链路。</summary>
        Public Shared Function XboxToMcProfile(msAccessToken As String, refreshToken As String) As McProfile
            ' 1) XBL
            Dim xblBody = "{""Properties"":{""AuthMethod"":""RPS"",""SiteName"":""user.auth.xboxlive.com"",""RpsTicket"":""d=" & msAccessToken & """},""RelyingParty"":""http://auth.xboxlive.com"",""TokenType"":""JWT""}"
            Dim xblResp = Http.PostJson("https://user.auth.xboxlive.com/user/authenticate", xblBody)
            Using d1 = JsonDocument.Parse(xblResp)
                Dim xblToken = d1.RootElement.GetProperty("Token").GetString()
                Dim uhs = d1.RootElement.GetProperty("DisplayClaims").GetProperty("xui")(0).GetProperty("uhs").GetString()

                ' 2) XSTS
                Dim xstsBody = "{""Properties"":{""SandboxId"":""RETAIL"",""UserTokens"":[""" & xblToken & """]},""RelyingParty"":""rp://api.minecraftservices.com/"",""TokenType"":""JWT""}"
                Dim xstsResp = Http.PostJson("https://xsts.auth.xboxlive.com/xsts/authorize", xstsBody)
                Using d2 = JsonDocument.Parse(xstsResp)
                    ' Xbox 认证错误（没有 Xbox 账户 / 未成年 / 被封等）会在这里返回 XErr
                    Dim xerrEl As JsonElement
                    If d2.RootElement.TryGetProperty("XErr", xerrEl) Then
                        Throw New Exception(XstsMessage(xerrEl.GetInt64()))
                    End If
                    Dim xstsToken = d2.RootElement.GetProperty("Token").GetString()

                    ' 3) Minecraft
                    Dim mcBody = "{""identityToken"":""XBL3.0 x=" & uhs & ";" & xstsToken & """}"
                    Dim mcResp = Http.PostJson("https://api.minecraftservices.com/authentication/login_with_xbox", mcBody)
                    Using d3 = JsonDocument.Parse(mcResp)
                        Dim mcToken = d3.RootElement.GetProperty("access_token").GetString()

                        ' 4) Profile —— 必须带 Bearer，否则一律 401
                        Dim profileJson As String
                        Try
                            profileJson = Http.GetStringAuth("https://api.minecraftservices.com/minecraft/profile", mcToken)
                        Catch ex As Exception
                            If ex.Message.Contains("404") Then
                                Throw New Exception("这个微软账号还没有 Minecraft：Java 版（未购买或未创建角色名）。" &
                                                    Environment.NewLine & "请先用官方启动器登录一次、建好角色名，再回来重试。")
                            End If
                            Throw
                        End Try
                        Using d4 = JsonDocument.Parse(profileJson)
                            Return New McProfile With {
                                .Name = d4.RootElement.GetProperty("name").GetString(),
                                .Uuid = d4.RootElement.GetProperty("id").GetString(),
                                .AccessToken = mcToken,
                                .RefreshToken = refreshToken
                            }
                        End Using
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>Xbox XSTS 错误码 → 人话。</summary>
        Private Shared Function XstsMessage(code As Long) As String
            Select Case code
                Case 2148916233
                    Return "这个微软账号还没有 Xbox 账户。" & Environment.NewLine &
                           "请先用它登录一次 xbox.com（免费创建），再回来重试。"
                Case 2148916235
                    Return "Xbox Live 在你所在的地区不可用。"
                Case 2148916236, 2148916237
                    Return "需要完成成人年龄验证：https://account.live.com/proofs/Manage"
                Case 2148916238
                    Return "这是未成年账号，需要家长在 Microsoft 家庭组里批准后才能登录。"
                Case 2148916227
                    Return "这个 Xbox 账号已被封禁。"
                Case Else
                    Return "Xbox 认证失败（XErr=" & code & "）。"
            End Select
        End Function

        ''' <summary>用 refresh_token 换新 MC 档案（静默续期）。</summary>
        Public Shared Function Refresh(clientId As String, refreshToken As String) As McProfile
            Dim form As New Dictionary(Of String, String) From {
                {"client_id", clientId},
                {"refresh_token", refreshToken},
                {"grant_type", "refresh_token"},
                {"scope", Scope}
            }
            Dim resp = Http.PostForm(TokenUrl, form)
            Using doc = JsonDocument.Parse(resp)
                Dim r = doc.RootElement
                Dim errEl As JsonElement
                If r.TryGetProperty("error", errEl) Then
                    Throw New Exception("刷新登录失败: " & errEl.GetString())
                End If
                Return XboxToMcProfile(r.GetProperty("access_token").GetString(),
                                       r.GetProperty("refresh_token").GetString())
            End Using
        End Function

    End Class

End Namespace
