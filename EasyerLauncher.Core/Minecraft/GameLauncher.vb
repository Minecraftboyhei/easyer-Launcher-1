Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports EasyerLauncher.Core.Config
Imports EasyerLauncher.Core.Net

Namespace Minecraft

    ''' <summary>游戏启动器：组装 JVM/游戏参数并拉起进程。</summary>
    Public Class GameLauncher

        Public Class LaunchOptions
            Public Property VersionId As String
            Public Property GameDir As String
            ''' <summary>versions / libraries / assets 的根目录。整合包实例用它指向全局游戏目录，GameDir 指向实例目录。</summary>
            Public Property RootDir As String = ""
            Public Property JavaPath As String = "java"
            Public Property MaxMemoryMB As Integer = 4096
            Public Property MinMemoryMB As Integer = 512
            Public Property Account As AccountInfo
            Public Property WindowWidth As Integer = 1280
            Public Property WindowHeight As Integer = 720
            Public Property FullScreen As Boolean = False
            Public Property ServerAddress As String = ""    ' 可选：直接进服务器
            Public Property AuthlibInjectorUrl As String = ""   ' 外置登录：Yggdrasil 根地址
            Public Property AuthlibInjectorJar As String = ""   ' 外置登录：agent jar 路径
        End Class

        ''' <summary>
        ''' 参数值含空格时补双引号。路径里带空格（如 "easyer Launcher 1"）不引起来会被 JVM 拆成多个参数，
        ''' 表现为主类被认成路径里的某一段（ClassNotFoundException: Launcher）。
        ''' </summary>
        Private Shared Function Q(token As String) As String
            If String.IsNullOrEmpty(token) Then Return token
            If token.Length >= 2 AndAlso token.StartsWith(""""c) AndAlso token.EndsWith(""""c) Then Return token
            If token.Contains(" "c) OrElse token.Contains(ControlChars.Tab) Then
                Return """" & token & """"
            End If
            Return token
        End Function

        ''' <summary>构建 java 命令行参数（不含 java 可执行文件本身）。</summary>
        Public Shared Function BuildArguments(opt As LaunchOptions) As String
            ' 整合包实例：版本库/资源库在全局根目录，游戏存档与 mods 在实例目录
            Dim root = If(String.IsNullOrEmpty(opt.RootDir), opt.GameDir, opt.RootDir)
            Dim versionsDir = Path.Combine(root, "versions")
            Dim rv = VersionResolver.Resolve(opt.VersionId, versionsDir)

            ' 先确保文件齐全
            GameInstaller.Install(opt.VersionId, root, Sub(s) System.Diagnostics.Debug.WriteLine(s))

            Dim acc = opt.Account
            If acc Is Nothing Then acc = New AccountInfo With {.Name = "Player", .Uuid = "00000000-0000-0000-0000-000000000000", .AccessToken = "0"}

            Dim assetsDir = Path.Combine(root, "assets")
            Dim libsDir = Path.Combine(root, "libraries")
            Dim nativesDir = rv.NativesDir
            If String.IsNullOrEmpty(nativesDir) Then nativesDir = Path.Combine(versionsDir, rv.Id, "natives")

            ' 类路径：本版本专属库 + 全部库 + 客户端 jar
            Dim cp As New StringBuilder()
            For Each libEntry In rv.Libraries
                If libEntry.IsNative Then Continue For
                Dim p = Path.Combine(libsDir, libEntry.Path.Replace("/"c, Path.DirectorySeparatorChar))
                If File.Exists(p) Then
                    If cp.Length > 0 Then cp.Append(";"c)
                    cp.Append(p)
                End If
            Next
            If cp.Length > 0 Then cp.Append(";"c)
            If Not String.IsNullOrEmpty(rv.ClientJarPath) AndAlso File.Exists(rv.ClientJarPath) Then
                cp.Append(rv.ClientJarPath)
            Else
                ' Fabric/Forge 的 profile 没有自己的 jar：继承链顶层的 jar
                Dim topJar = Path.Combine(versionsDir, rv.Id, rv.Id & ".jar")
                If File.Exists(topJar) Then
                    cp.Append(topJar)
                Else
                    Throw New Exception("找不到客户端 jar，请先安装版本")
                End If
            End If

            Dim args As New List(Of String)

            ' ==== JVM 参数 ====
            args.Add("-Xms" & opt.MinMemoryMB & "M")
            args.Add("-Xmx" & opt.MaxMemoryMB & "M")
            args.Add("-XX:+UseG1GC")
            args.Add("-XX:+UnlockExperimentalVMOptions")
            args.Add("-Dfile.encoding=UTF-8")

            ' 外置登录：挂 authlib-injector agent（必须在 -cp 之前）
            If Not String.IsNullOrEmpty(opt.AuthlibInjectorUrl) Then
                args.Add(Q("-javaagent:" & opt.AuthlibInjectorJar & "=" & opt.AuthlibInjectorUrl))
            End If

            If rv.JvmArgs.Count > 0 Then
                For Each token In rv.JvmArgs
                    args.Add(Q(Substitute(token, opt, rv, cp.ToString(), nativesDir, assetsDir)))
                Next
            Else
                args.Add(Q("-Djava.library.path=" & nativesDir))
                args.Add("-cp")
                args.Add(Q(cp.ToString()))
            End If

            ' 日志配置
            If rv.LoggingArgument <> "" AndAlso rv.LoggingFilePath <> "" Then
                Dim logPath = Path.Combine(root, rv.LoggingFilePath.Replace("/"c, Path.DirectorySeparatorChar))
                If File.Exists(logPath) Then
                    args.Add(Q(rv.LoggingArgument.Replace("${path}", logPath)))
                End If
            End If

            args.Add(rv.MainClass)

            ' ==== 游戏参数 ====
            If rv.MinecraftArguments <> "" Then
                For Each token In rv.MinecraftArguments.Split(" "c)
                    If token.Trim() = "" Then Continue For
                    args.Add(Q(Substitute(token, opt, rv, cp.ToString(), nativesDir, assetsDir)))
                Next
            ElseIf rv.GameArgs.Count > 0 Then
                For Each token In rv.GameArgs
                    args.Add(Q(Substitute(token, opt, rv, cp.ToString(), nativesDir, assetsDir)))
                Next
            Else
                Throw New Exception("版本 JSON 中没有游戏参数")
            End If

            ' 分辨率（值与开关分开成两个参数，否则 " 会把两者包成一个）
            If opt.FullScreen Then
                args.Add("--fullscreen")
            Else
                args.Add("--width")
                args.Add(opt.WindowWidth.ToString())
                args.Add("--height")
                args.Add(opt.WindowHeight.ToString())
            End If

            If opt.ServerAddress <> "" Then
                args.Add("--server")
                args.Add(opt.ServerAddress.Split(":"c)(0))
                If opt.ServerAddress.Contains(":") Then
                    args.Add("--port")
                    args.Add(opt.ServerAddress.Split(":"c)(1))
                End If
            End If

            Return String.Join(" ", args)
        End Function

        Private Shared Function Substitute(token As String, opt As LaunchOptions, rv As ResolvedVersion,
                                           classpath As String, nativesDir As String, assetsDir As String) As String
            Dim acc = If(opt.Account, New AccountInfo With {.Name = "Player", .Uuid = New String("0"c, 36), .AccessToken = "0"})
            Dim s = token
            s = s.Replace("${auth_player_name}", acc.Name)
            s = s.Replace("${auth_uuid}", If(acc.Uuid = "", New String("0"c, 36), acc.Uuid))
            s = s.Replace("${auth_access_token}", If(acc.AccessToken = "", "0", acc.AccessToken))
            s = s.Replace("${auth_session}", If(acc.AccessToken = "", "0", acc.AccessToken))
            s = s.Replace("${auth_xuid}", "0")
            s = s.Replace("${clientid}", "easyer")
            s = s.Replace("${user_type}", If(acc.AccountType = "microsoft", "msa", "legacy"))
            s = s.Replace("${user_properties}", "{}")
            s = s.Replace("${version_name}", rv.Id)
            s = s.Replace("${version_type}", "release")
            s = s.Replace("${game_directory}", opt.GameDir)
            s = s.Replace("${assets_root}", assetsDir)
            s = s.Replace("${game_assets}", Path.Combine(assetsDir, "virtual", "legacy"))
            s = s.Replace("${assets_index_name}", rv.AssetIndexId)
            s = s.Replace("${natives_directory}", nativesDir)
            s = s.Replace("${launcher_name}", "EasyerLauncher")
            s = s.Replace("${launcher_version}", "1.0")
            s = s.Replace("${classpath}", classpath)
            Dim rootDir = If(String.IsNullOrEmpty(opt.RootDir), opt.GameDir, opt.RootDir)
            s = s.Replace("${library_directory}", Path.Combine(rootDir, "libraries"))
            s = s.Replace("${classpath_separator}", ";")
            Return s
        End Function

        ''' <summary>启动游戏。若进程几秒内秒退，自动重跑一次抓取错误输出并抛出可读原因。</summary>
        Public Shared Function Launch(opt As LaunchOptions) As Process
            Dim args = BuildArguments(opt)
            Dim psi As New ProcessStartInfo With {
                .FileName = opt.JavaPath,
                .Arguments = args,
                .WorkingDirectory = opt.GameDir,
                .UseShellExecute = False,
                .CreateNoWindow = False,
                .RedirectStandardOutput = False,
                .RedirectStandardError = False
            }
            Dim p = Process.Start(psi)

            ' 3.5 秒内退出 = 没能起来（正常启动此时早就在加载资源了）
            If p.WaitForExit(3500) Then
                Dim diag = CaptureFailure(psi)
                Dim hint As String = ""
                If diag.Contains("ClassNotFoundException") OrElse diag.Contains("找不到或无法加载主类") Then
                    hint = Environment.NewLine & "提示：路径里带空格或特殊字符会导致参数被拆开，建议把启动器和游戏目录换个不含空格的路径。"
                ElseIf diag.Contains("UnsupportedClassVersionError") Then
                    hint = Environment.NewLine & "提示：Java 版本太低，请换用新版 Java（21 及以上）。"
                ElseIf diag.Contains("OutOfMemoryError") Then
                    hint = Environment.NewLine & "提示：内存不足，去设置页把内存调小一点。"
                End If
                Throw New Exception("游戏进程启动后立即退出（退出码 " & p.ExitCode & "）。" & hint &
                                    Environment.NewLine & Environment.NewLine & "Java 输出：" &
                                    Environment.NewLine & diag)
            End If
            Return p
        End Function

        ''' <summary>重跑一次并捕获输出，用于把秒退原因翻出来（最多 12 秒）。</summary>
        Private Shared Function CaptureFailure(psi As ProcessStartInfo) As String
            Dim sb As New StringBuilder()
            Try
                Dim psi2 As New ProcessStartInfo With {
                    .FileName = psi.FileName,
                    .Arguments = psi.Arguments,
                    .WorkingDirectory = psi.WorkingDirectory,
                    .UseShellExecute = False,
                    .CreateNoWindow = True,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True
                }
                Dim onData As DataReceivedEventHandler =
                    Sub(s, e)
                        If e.Data IsNot Nothing Then
                            SyncLock sb
                                sb.AppendLine(e.Data)
                            End SyncLock
                        End If
                    End Sub
                Using p2 = Process.Start(psi2)
                    AddHandler p2.OutputDataReceived, onData
                    AddHandler p2.ErrorDataReceived, onData
                    p2.BeginOutputReadLine()
                    p2.BeginErrorReadLine()
                    If Not p2.WaitForExit(12000) Then
                        Try
                            p2.Kill()
                        Catch
                        End Try
                    End If
                End Using
            Catch ex As Exception
                Return "（无法读取 Java 输出：" & ex.Message & "）"
            End Try

            Dim text As String
            SyncLock sb
                text = sb.ToString()
            End SyncLock
            If String.IsNullOrWhiteSpace(text) Then Return "（Java 没有输出任何内容，可能是 Java 路径不对）"
            ' 只留下最后 30 行，避免弹窗过长
            Dim arr = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim from = Math.Max(0, arr.Length - 30)
            Dim sbOut As New StringBuilder()
            For i = from To arr.Length - 1
                sbOut.AppendLine(arr(i))
            Next
            Return sbOut.ToString()
        End Function

    End Class

End Namespace
