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
            Public Property JavaPath As String = "java"
            Public Property MaxMemoryMB As Integer = 4096
            Public Property MinMemoryMB As Integer = 512
            Public Property Account As AccountInfo
            Public Property WindowWidth As Integer = 1280
            Public Property WindowHeight As Integer = 720
            Public Property FullScreen As Boolean = False
            Public Property ServerAddress As String = ""    ' 可选：直接进服务器
        End Class

        ''' <summary>构建 java 命令行参数（不含 java 可执行文件本身）。</summary>
        Public Shared Function BuildArguments(opt As LaunchOptions) As String
            Dim versionsDir = Path.Combine(opt.GameDir, "versions")
            Dim rv = VersionResolver.Resolve(opt.VersionId, versionsDir)

            ' 先确保文件齐全
            GameInstaller.Install(opt.VersionId, opt.GameDir, Sub(s) System.Diagnostics.Debug.WriteLine(s))

            Dim acc = opt.Account
            If acc Is Nothing Then acc = New AccountInfo With {.Name = "Player", .Uuid = "00000000-0000-0000-0000-000000000000", .AccessToken = "0"}

            Dim assetsDir = Path.Combine(opt.GameDir, "assets")
            Dim libsDir = Path.Combine(opt.GameDir, "libraries")
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

            If rv.JvmArgs.Count > 0 Then
                For Each token In rv.JvmArgs
                    args.Add(Substitute(token, opt, rv, cp.ToString(), nativesDir, assetsDir))
                Next
            Else
                args.Add("-Djava.library.path=""" & nativesDir & """")
                args.Add("-cp """ & cp.ToString() & """")
            End If

            ' 日志配置
            If rv.LoggingArgument <> "" AndAlso rv.LoggingFilePath <> "" Then
                Dim logPath = Path.Combine(opt.GameDir, rv.LoggingFilePath.Replace("/"c, Path.DirectorySeparatorChar))
                If File.Exists(logPath) Then
                    args.Add(rv.LoggingArgument.Replace("${path}", logPath))
                End If
            End If

            args.Add(rv.MainClass)

            ' ==== 游戏参数 ====
            If rv.MinecraftArguments <> "" Then
                For Each token In rv.MinecraftArguments.Split(" "c)
                    If token.Trim() = "" Then Continue For
                    args.Add(Substitute(token, opt, rv, cp.ToString(), nativesDir, assetsDir))
                Next
            ElseIf rv.GameArgs.Count > 0 Then
                For Each token In rv.GameArgs
                    args.Add(Substitute(token, opt, rv, cp.ToString(), nativesDir, assetsDir))
                Next
            Else
                Throw New Exception("版本 JSON 中没有游戏参数")
            End If

            ' 分辨率
            If opt.FullScreen Then
                args.Add("--fullscreen")
            Else
                args.Add("--width " & opt.WindowWidth)
                args.Add("--height " & opt.WindowHeight)
            End If

            If opt.ServerAddress <> "" Then
                args.Add("--server " & opt.ServerAddress.Split(":"c)(0))
                If opt.ServerAddress.Contains(":") Then args.Add("--port " & opt.ServerAddress.Split(":"c)(1))
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
            s = s.Replace("${library_directory}", Path.Combine(opt.GameDir, "libraries"))
            s = s.Replace("${classpath_separator}", ";")
            Return s
        End Function

        ''' <summary>启动游戏，返回进程。</summary>
        Public Shared Function Launch(opt As LaunchOptions) As Process
            Dim workingDir As String = Nothing
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
            Return Process.Start(psi)
        End Function

    End Class

End Namespace
