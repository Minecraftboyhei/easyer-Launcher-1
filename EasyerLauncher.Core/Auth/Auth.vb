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

        ''' <summary>
        ''' 本项目自带的 Azure 应用 Client ID。公共客户端 ID 不是机密，可以随程序公开分发。
        ''' 该应用已配置为「任何组织目录中的账户和个人 Microsoft 账户」+ 公共客户端流 + http://localhost/signin。
        ''' </summary>
        Public Const DefaultClientId As String = "ee60d27a-fb39-4e43-a2c5-6d04f5e4a5b4"

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

        ''' <summary>生成浏览器授权地址（授权码模式 + localhost 回调，端口由 MS 回环规则忽略）。</summary>
        Public Shared Function BuildAuthorizeUrl(clientId As String, port As Integer) As String
            Return "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize" &
                   "?client_id=" & Uri.EscapeDataString(clientId) &
                   "&response_type=code" &
                   "&redirect_uri=" & Uri.EscapeDataString("http://localhost:" & port & "/signin") &
                   "&response_mode=query" &
                   "&scope=" & Uri.EscapeDataString(Scope)
        End Function

        ''' <summary>用授权码换令牌 → XBL → MC 档案。</summary>
        Public Shared Function FinishWithAuthCode(clientId As String, code As String, port As Integer) As McProfile
            Dim form As New Dictionary(Of String, String) From {
                {"client_id", clientId},
                {"code", code},
                {"redirect_uri", "http://localhost:" & port & "/signin"},
                {"grant_type", "authorization_code"}
            }
            Dim resp = Http.PostForm(TokenUrl, form)
            Using doc = JsonDocument.Parse(resp)
                Dim r = doc.RootElement
                Dim errEl As JsonElement
                If r.TryGetProperty("error", errEl) Then
                    Dim desc = ""
                    Dim dEl As JsonElement
                    If r.TryGetProperty("error_description", dEl) Then desc = dEl.GetString()
                    Throw New Exception("微软登录错误: " & errEl.GetString() & If(desc <> "", " — " & desc, ""))
                End If
                Dim refresh As String = ""
                Dim rfEl As JsonElement
                If r.TryGetProperty("refresh_token", rfEl) Then refresh = rfEl.GetString()
                Dim atEl As JsonElement
                If Not r.TryGetProperty("access_token", atEl) Then
                    Throw New Exception("微软令牌兑换返回异常（缺少 access_token）。" &
                                        Environment.NewLine & "原始响应：" & If(resp.Length > 500, resp.Substring(0, 500) & "…", resp))
                End If
                Return XboxToMcProfile(atEl.GetString(), refresh)
            End Using
        End Function

        ''' <summary>用 MS access_token 完成 XBL→XSTS→MC 链路。</summary>
        Public Shared Function XboxToMcProfile(msAccessToken As String, refreshToken As String) As McProfile
            ' 1) XBL —— 注意：Xbox 账户问题（没有 Xbox/未成年/封禁）在这一步就会返回 XErr
            Dim xblBody = "{""Properties"":{""AuthMethod"":""RPS"",""SiteName"":""user.auth.xboxlive.com"",""RpsTicket"":""d=" & msAccessToken & """},""RelyingParty"":""http://auth.xboxlive.com"",""TokenType"":""JWT""}"
            Dim xblResp = Http.PostJson("https://user.auth.xboxlive.com/user/authenticate", xblBody)
            Using d1 = JsonDocument.Parse(xblResp)
                ThrowXErrIfPresent(d1.RootElement, "Xbox Live")
                Dim xblToken = GetStr(d1.RootElement, "Token", "Xbox Live 第一步", xblResp)
                Dim uhs = GetStr(d1.RootElement.GetProperty("DisplayClaims").GetProperty("xui")(0), "uhs", "Xbox 用户标识", xblResp)

                ' 2) XSTS
                Dim xstsBody = "{""Properties"":{""SandboxId"":""RETAIL"",""UserTokens"":[""" & xblToken & """]},""RelyingParty"":""rp://api.minecraftservices.com/"",""TokenType"":""JWT""}"
                Dim xstsResp = Http.PostJson("https://xsts.auth.xboxlive.com/xsts/authorize", xstsBody)
                Using d2 = JsonDocument.Parse(xstsResp)
                    ' Xbox 认证错误（没有 Xbox 账户 / 未成年 / 被封等）会在这里返回 XErr
                    ThrowXErrIfPresent(d2.RootElement, "Xbox 安全令牌")
                    Dim xstsToken = GetStr(d2.RootElement, "Token", "Xbox 安全令牌", xstsResp)

                    ' 3) Minecraft
                    Dim mcBody = "{""identityToken"":""XBL3.0 x=" & uhs & ";" & xstsToken & """}"
                    Dim mcResp = Http.PostJson("https://api.minecraftservices.com/authentication/login_with_xbox", mcBody)
                    Using d3 = JsonDocument.Parse(mcResp)
                        ' Mojang 对 login_with_xbox 实行 Client ID 白名单：自建 Azure 应用必须先通过官方审核
                        If mcResp.Contains("Invalid app registration") Then
                            Throw New Exception(
                                "你的微软账号、Xbox 认证都已通过 ✓，但最后一步被 Mojang 拦下：" &
                                Environment.NewLine &
                                "Mojang 要求所有第三方启动器的应用先通过「Java Edition API 审核」进入白名单，" &
                                "自建的 Azure 应用默认不在名单里（这不是配置错误，任何设置都改不了）。" &
                                Environment.NewLine & Environment.NewLine &
                                "解决办法：" & Environment.NewLine &
                                "① 到 https://aka.ms/mce-reviewappid 提交 API 审核申请，" &
                                "填上 Client ID：" & "（见账号页输入框），审核几天到几周，通过后本界面即正常；" &
                                Environment.NewLine &
                                "② 现在就想玩：用账号页的「LittleSkin 外置登录」或「离线登录」。")
                        End If
                        Dim mcToken = GetStr(d3.RootElement, "access_token", "Minecraft 登录", mcResp)

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
                                .Name = GetStr(d4.RootElement, "name", "Minecraft 档案", profileJson),
                                .Uuid = GetStr(d4.RootElement, "id", "Minecraft 档案", profileJson),
                                .AccessToken = mcToken,
                                .RefreshToken = refreshToken
                            }
                        End Using
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>Xbox 错误响应里带 XErr 就转成人话抛出。</summary>
        Private Shared Sub ThrowXErrIfPresent(root As JsonElement, stage As String)
            Dim xerrEl As JsonElement
            If root.TryGetProperty("XErr", xerrEl) Then
                Throw New Exception(XstsMessage(xerrEl.GetInt64()))
            End If
        End Sub

        ''' <summary>安全取 JSON 字符串字段，缺键时报出阶段名和原始响应（不再裸抛字典错误）。</summary>
        Private Shared Function GetStr(el As JsonElement, name As String, stage As String, raw As String) As String
            Dim v As JsonElement
            If el.TryGetProperty(name, v) Then Return v.GetString()
            Throw New Exception(stage & " 返回异常（缺少 " & name & " 字段）。" &
                                Environment.NewLine & "原始响应：" & If(raw.Length > 500, raw.Substring(0, 500) & "…", raw))
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
