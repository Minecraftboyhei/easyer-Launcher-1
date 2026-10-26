Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading

Namespace Auth

    ''' <summary>
    ''' 浏览器授权码登录：打开系统浏览器 → 微软授权后回调 http://localhost:随机端口/signin
    ''' → 本地抓取授权码 → 换令牌完成 XBL→MC 链路。全程不用手输代码。
    ''' </summary>
    Public Class LoopbackLogin

        ''' <summary>执行一次浏览器登录。timeoutSec 内未完成则抛异常。port=0 时自动选随机空闲端口。</summary>
        Public Shared Function Run(clientId As String, timeoutSec As Integer,
                                   Optional port As Integer = 0,
                                   Optional openBrowser As Boolean = True) As MicrosoftAuth.McProfile
            ' 1) 监听（指定端口或随机空闲端口）
            Dim listener As TcpListener = If(port > 0, New TcpListener(IPAddress.Loopback, port), New TcpListener(IPAddress.Loopback, 0))
            listener.Start()
            port = CType(listener.LocalEndpoint, IPEndPoint).Port

            ' 2) 拉起系统浏览器
            Dim url = MicrosoftAuth.BuildAuthorizeUrl(clientId, port)
            If openBrowser Then
                Try
                    Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
                Catch
                    listener.Stop()
                    Throw New Exception("无法打开浏览器。请手动访问：" & Environment.NewLine & url)
                End Try
            End If

            ' 3) 等回调连接，解析授权码（浏览器可能连多次，如 favicon）
            Dim deadline = DateTime.UtcNow.AddSeconds(timeoutSec)
            Dim code As String = Nothing
            Try
                While code Is Nothing
                    If DateTime.UtcNow > deadline Then
                        Throw New Exception("等待浏览器授权超时（" & timeoutSec & " 秒），请重试。")
                    End If
                    If Not listener.Pending() Then
                        Thread.Sleep(150)
                        Continue While
                    End If

                    Dim errorDesc As String = Nothing
                    Using client = listener.AcceptTcpClient()
                        Dim request = ReadRequest(client)
                        code = ExtractQueryParam(request, "code")
                        errorDesc = ExtractQueryParam(request, "error_description")

                        Dim html As String
                        If code IsNot Nothing Then
                            html = "<html><body style='font-family:system-ui;text-align:center;padding-top:60px'>" &
                                   "<h2>&#10003; 登录成功</h2><p>可以关闭这个页面，回到启动器。</p></body></html>"
                        ElseIf errorDesc IsNot Nothing Then
                            html = "<html><body style='font-family:system-ui;text-align:center;padding-top:60px'>" &
                                   "<h2>登录被取消</h2><p>回到启动器重新点登录即可。</p></body></html>"
                        Else
                            html = "<html><body style='font-family:system-ui;text-align:center;padding-top:60px'>" &
                                   "<h2>..</h2></body></html>"   ' favicon 之类的杂项连接
                        End If
                        WriteResponse(client, html)
                    End Using

                    If code Is Nothing AndAlso errorDesc IsNot Nothing Then
                        Throw New Exception("你在浏览器里取消了授权。" & Environment.NewLine & errorDesc)
                    End If
                End While
            Finally
                listener.Stop()
            End Try

            ' 4) 授权码 → 令牌 → MC 档案
            Return MicrosoftAuth.FinishWithAuthCode(clientId, code, port)
        End Function

        Private Shared Function ReadRequest(client As TcpClient) As String
            client.ReceiveTimeout = 5000
            Dim stream = client.GetStream()
            Dim sb As New StringBuilder()
            Dim buf(8191) As Byte
            Dim read As Integer
            Try
                read = stream.Read(buf, 0, buf.Length)
                While read > 0 AndAlso sb.Length < 16384
                    sb.Append(Encoding.ASCII.GetString(buf, 0, read))
                    If sb.ToString().Contains(vbCr & vbLf & vbCr & vbLf) Then Exit While
                    If Not stream.DataAvailable Then Exit While
                    read = stream.Read(buf, 0, buf.Length)
                End While
            Catch
            End Try
            Return sb.ToString()
        End Function

        Private Shared Function ExtractQueryParam(request As String, name As String) As String
            ' "GET /signin?code=xxx&scope=... HTTP/1.1"
            Try
                Dim parts = request.Split(" "c)
                If parts.Length < 2 Then Return Nothing
                Dim target = parts(1)
                Dim qs = target.IndexOf("?"c)
                If qs < 0 Then Return Nothing
                For Each kv In target.Substring(qs + 1).Split("&"c)
                    Dim pair = kv.Split({"="c}, 2)
                    If pair.Length = 2 AndAlso pair(0) = name Then
                        Return Uri.UnescapeDataString(pair(1).Replace("+"c, " "c))
                    End If
                Next
            Catch
            End Try
            Return Nothing
        End Function

        Private Shared Sub WriteResponse(client As TcpClient, html As String)
            Try
                Dim stream = client.GetStream()
                Dim body = Encoding.UTF8.GetBytes(html)
                Dim header = "HTTP/1.1 200 OK" & vbCr & vbLf &
                             "Content-Type: text/html; charset=utf-8" & vbCr & vbLf &
                             "Content-Length: " & body.Length & vbCr & vbLf &
                             "Connection: close" & vbCr & vbLf & vbCr & vbLf
                Dim head = Encoding.ASCII.GetBytes(header)
                stream.Write(head, 0, head.Length)
                stream.Write(body, 0, body.Length)
                stream.Flush()
                Thread.Sleep(100)   ' 给内核一点时间把数据推出去
            Catch
            End Try
        End Sub

    End Class

End Namespace
