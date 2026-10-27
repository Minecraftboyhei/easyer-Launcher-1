Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading

Namespace Net

    ''' <summary>全局 HttpClient 与下载工具。</summary>
    Public Class Http

        ''' <summary>长超时客户端：大文件下载用。</summary>
        Public Shared ReadOnly Client As HttpClient

        ''' <summary>短超时客户端：API 请求 / 图标等小资源，防止界面卡死在"搜索中"。</summary>
        Public Shared ReadOnly ApiClient As HttpClient

        Shared Sub New()
            ' 显式跟随环境代理（与 curl 行为一致），避免系统代理端口失效时请求挂死
            Dim proxyUrl = Environment.GetEnvironmentVariable("HTTPS_PROXY")
            If String.IsNullOrEmpty(proxyUrl) Then proxyUrl = Environment.GetEnvironmentVariable("https_proxy")
            If String.IsNullOrEmpty(proxyUrl) Then proxyUrl = Environment.GetEnvironmentVariable("HTTP_PROXY")
            If String.IsNullOrEmpty(proxyUrl) Then proxyUrl = Environment.GetEnvironmentVariable("http_proxy")

            Dim ua = "EasyerLauncher/1.0 (Minecraft Launcher)"
            If Not String.IsNullOrEmpty(proxyUrl) Then
                Dim handler As New HttpClientHandler With {.Proxy = New System.Net.WebProxy(proxyUrl), .UseProxy = True}
                Client = New HttpClient(handler) With {.Timeout = TimeSpan.FromMinutes(10)}
                ApiClient = New HttpClient(handler) With {.Timeout = TimeSpan.FromSeconds(15)}
            Else
                Client = New HttpClient With {.Timeout = TimeSpan.FromMinutes(10)}
                ApiClient = New HttpClient With {.Timeout = TimeSpan.FromSeconds(15)}
            End If
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(ua)
            ApiClient.DefaultRequestHeaders.UserAgent.ParseAdd(ua)
        End Sub

        ''' <summary>GET 并自动重试一次（对付代理偶发挂起）。retry=False 时快速失败，供多源切换使用。</summary>
        Public Shared Function GetString(url As String, Optional timeoutSec As Integer = 20,
                                        Optional retry As Boolean = True) As String
            If Not retry Then Return GetStringOnce(url, timeoutSec)
            Try
                Return GetStringOnce(url, timeoutSec)
            Catch ex As Exception When TypeOf ex Is OperationCanceledException OrElse
                                       ex.InnerException IsNot Nothing AndAlso TypeOf ex.InnerException Is OperationCanceledException
                ' 超时 → 重试一次
                Return GetStringOnce(url, timeoutSec)
            End Try
        End Function

        Private Shared Function GetStringOnce(url As String, timeoutSec As Integer) As String
            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec))
                Using req As New HttpRequestMessage(HttpMethod.Get, url)
                    Using resp = Client.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).GetAwaiter().GetResult()
                        resp.EnsureSuccessStatusCode()
                        Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>带 Bearer 令牌的 GET。</summary>
        Public Shared Function GetStringAuth(url As String, bearer As String, Optional timeoutSec As Integer = 20) As String
            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec))
                Using req As New HttpRequestMessage(HttpMethod.Get, url)
                    req.Headers.Authorization = New Headers.AuthenticationHeaderValue("Bearer", bearer)
                    Using resp = Client.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).GetAwaiter().GetResult()
                        If Not resp.IsSuccessStatusCode Then
                            Throw New Exception("HTTP " & CInt(resp.StatusCode) & " — " & url)
                        End If
                        Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>GET 并解析为 JsonDocument（调用方负责 Dispose）。</summary>
        Public Shared Function GetJson(url As String) As JsonDocument
            Return JsonDocument.Parse(GetString(url))
        End Function

        ''' <summary>POST JSON 并返回响应文本。</summary>
        Public Shared Function PostJson(url As String, json As String, Optional bearer As String = Nothing) As String
            Using content As New StringContent(json, Encoding.UTF8, "application/json")
                Using req As New HttpRequestMessage(HttpMethod.Post, url)
                    req.Content = content
                    If bearer IsNot Nothing Then req.Headers.Authorization = New Headers.AuthenticationHeaderValue("Bearer", bearer)
                    Using resp = Client.SendAsync(req).GetAwaiter().GetResult()
                        Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>POST 表单并返回响应文本。</summary>
        Public Shared Function PostForm(url As String, data As Dictionary(Of String, String)) As String
            Using content As New FormUrlEncodedContent(data)
                Using resp = Client.PostAsync(url, content).GetAwaiter().GetResult()
                    Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                End Using
            End Using
        End Function

        ''' <summary>BMCLAPI 国内镜像根（bangbang93 社区维护）。</summary>
        Public Const BmclapiBase As String = "https://bmclapi2.bangbang93.com/"

        ''' <summary>把官方资源 URL 转成 BMCLAPI 镜像 URL；无法镜像的返回 Nothing。</summary>
        Public Shared Function ToMirror(url As String) As String
            If String.IsNullOrEmpty(url) Then Return Nothing
            Dim hostPrefixes() As String = {
                "https://piston-meta.mojang.com/", "https://piston-data.mojang.com/",
                "https://launcher.mojang.com/", "https://launchermeta.mojang.com/"}
            For Each p In hostPrefixes
                If url.StartsWith(p, StringComparison.Ordinal) Then
                    Return BmclapiBase & url.Substring(p.Length)
                End If
            Next
            Const resPrefix = "https://resources.download.minecraft.net/"
            If url.StartsWith(resPrefix, StringComparison.Ordinal) Then
                Return BmclapiBase & "assets/" & url.Substring(resPrefix.Length)
            End If
            Const libPrefix = "https://libraries.minecraft.net/"
            If url.StartsWith(libPrefix, StringComparison.Ordinal) Then
                Return BmclapiBase & "maven/" & url.Substring(libPrefix.Length)
            End If
            Return Nothing
        End Function

        ''' <summary>GET 文本：国内镜像优先，失败回退官方。</summary>
        Public Shared Function GetStringAuto(url As String, Optional timeoutSec As Integer = 20) As String
            Dim mirror = ToMirror(url)
            If mirror Is Nothing Then Return GetString(url, timeoutSec)
            Try
                Return GetString(mirror, timeoutSec)
            Catch
                Return GetString(url, timeoutSec)
            End Try
        End Function

        ''' <summary>下载文件：国内镜像优先，失败回退官方，双源都炸才报错。
        ''' timeoutSec > 0 时限制单次请求超时（小文件用，避免挂起拖死整批下载）。</summary>
        Public Shared Sub DownloadFileAuto(url As String, destPath As String,
                                           Optional expectedSha1 As String = Nothing,
                                           Optional progress As Action(Of Long, Long) = Nothing,
                                           Optional timeoutSec As Integer = 0)
            Dim mirror = ToMirror(url)
            If mirror Is Nothing Then
                DownloadFile(url, destPath, expectedSha1, progress, timeoutSec)
                Return
            End If
            Dim mirrorEx As Exception = Nothing
            Try
                DownloadFile(mirror, destPath, expectedSha1, progress, timeoutSec)
                Return
            Catch ex As Exception
                mirrorEx = ex
            End Try
            Try
                DownloadFile(url, destPath, expectedSha1, progress, timeoutSec)
            Catch officialEx As Exception
                Throw New Exception("镜像与官方源均下载失败：" & url & Environment.NewLine &
                                    "  镜像: " & mirrorEx.Message & Environment.NewLine &
                                    "  官方: " & officialEx.Message)
            End Try
        End Sub

        ''' <summary>下载文件到指定路径，带 SHA1 校验与进度回调。timeoutSec > 0 覆盖默认 10 分钟超时。</summary>
        Public Shared Sub DownloadFile(url As String, destPath As String,
                                       Optional expectedSha1 As String = Nothing,
                                       Optional progress As Action(Of Long, Long) = Nothing,
                                       Optional timeoutSec As Integer = 0)
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destPath))

            If expectedSha1 IsNot Nothing AndAlso System.IO.File.Exists(destPath) Then
                If VerifySha1(destPath, expectedSha1) Then
                    If progress IsNot Nothing Then progress(New System.IO.FileInfo(destPath).Length, New System.IO.FileInfo(destPath).Length)
                    Return
                End If
            End If

            Dim tmpPath = destPath & ".download"
            Using cts As CancellationTokenSource = If(timeoutSec > 0, New CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec)), Nothing)
                Try
                    Using resp = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead,
                                                 If(cts Is Nothing, CancellationToken.None, cts.Token)).GetAwaiter().GetResult()
                        If Not resp.IsSuccessStatusCode Then
                            Throw New Exception("HTTP " & CInt(resp.StatusCode) & " — " & url)
                        End If
                        Dim total As Long = If(resp.Content.Headers.ContentLength.HasValue, resp.Content.Headers.ContentLength.Value, -1L)
                        Using fs = System.IO.File.Create(tmpPath)
                            Using stream = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                                Dim buffer(81919) As Byte
                                Dim read As Integer
                                Dim done As Long = 0
                                read = stream.Read(buffer, 0, buffer.Length)
                                While read > 0
                                    fs.Write(buffer, 0, read)
                                    done += read
                                    If progress IsNot Nothing AndAlso total > 0 Then progress(done, total)
                                    read = stream.Read(buffer, 0, buffer.Length)
                                End While
                            End Using
                        End Using
                    End Using
                Catch
                    ' 失败/超时/挂起都清掉半截文件，别留脏数据
                    Try
                        If System.IO.File.Exists(tmpPath) Then System.IO.File.Delete(tmpPath)
                    Catch
                    End Try
                    Throw
                End Try
            End Using

            If expectedSha1 IsNot Nothing AndAlso Not VerifySha1(tmpPath, expectedSha1) Then
                System.IO.File.Delete(tmpPath)
                Throw New Exception("SHA1 校验失败: " & url)
            End If

            If System.IO.File.Exists(destPath) Then System.IO.File.Delete(destPath)
            System.IO.File.Move(tmpPath, destPath)
        End Sub

        Public Shared Function VerifySha1(path As String, expected As String) As Boolean
            Using sha = System.Security.Cryptography.SHA1.Create()
                Using fs = System.IO.File.OpenRead(path)
                    Dim hash = sha.ComputeHash(fs)
                    Dim sb As New StringBuilder()
                    For Each b In hash
                        sb.Append(b.ToString("x2"))
                    Next
                    Return sb.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase)
                End Using
            End Using
        End Function

    End Class

End Namespace
