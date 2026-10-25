Imports System.Net.Http
Imports System.Text
Imports System.Text.Json

Namespace Net

    ''' <summary>全局 HttpClient 与下载工具。</summary>
    Public Class Http

        Public Shared ReadOnly Client As New HttpClient With {
            .Timeout = TimeSpan.FromMinutes(10)
        }

        Shared Sub New()
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("EasyerLauncher/1.0 (Minecraft Launcher)")
        End Sub

        ''' <summary>GET 文本内容。</summary>
        Public Shared Function GetString(url As String) As String
            Return Client.GetStringAsync(url).GetAwaiter().GetResult()
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

        ''' <summary>下载文件到指定路径，带 SHA1 校验与进度回调。</summary>
        Public Shared Sub DownloadFile(url As String, destPath As String,
                                       Optional expectedSha1 As String = Nothing,
                                       Optional progress As Action(Of Long, Long) = Nothing)
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destPath))

            If expectedSha1 IsNot Nothing AndAlso System.IO.File.Exists(destPath) Then
                If VerifySha1(destPath, expectedSha1) Then
                    If progress IsNot Nothing Then progress(New System.IO.FileInfo(destPath).Length, New System.IO.FileInfo(destPath).Length)
                    Return
                End If
            End If

            Dim tmpPath = destPath & ".download"
            Using resp = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult()
                resp.EnsureSuccessStatusCode()
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
