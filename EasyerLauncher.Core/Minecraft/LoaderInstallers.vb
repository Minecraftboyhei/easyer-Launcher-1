Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Minecraft

    ''' <summary>Fabric / Quilt 加载器安装（通过官方 meta API 生成继承版 profile）。</summary>
    Public Class FabricInstaller

        Public Enum LoaderKind
            Fabric = 0
            Quilt = 1
        End Enum

        ''' <summary>获取指定游戏版本的加载器版本列表。</summary>
        Public Shared Function GetLoaderVersions(kind As LoaderKind, gameVersion As String) As List(Of String)
            Dim url As String
            Dim prop As String
            If kind = LoaderKind.Fabric Then
                url = "https://meta.fabricmc.net/v2/versions/loader/" & gameVersion
                prop = "version"
            Else
                url = "https://meta.quiltmc.org/v3/versions/loader/" & gameVersion
                prop = "version"
            End If
            Dim result As New List(Of String)
            Using doc = Http.GetJson(url)
                For Each item In doc.RootElement.EnumerateArray()
                    Dim loader As JsonElement
                    If item.TryGetProperty("loader", loader) Then
                        result.Add(loader.GetProperty(prop).GetString())
                    Else
                        result.Add(item.GetProperty(prop).GetString())
                    End If
                Next
            End Using
            Return result
        End Function

        ''' <summary>安装加载器：下载 profile JSON 与其全部库文件。</summary>
        Public Shared Sub Install(kind As LoaderKind, gameVersion As String, loaderVersion As String,
                                  gameDir As String, report As Action(Of String))
            Dim url As String
            If kind = LoaderKind.Fabric Then
                url = String.Format("https://meta.fabricmc.net/v2/versions/loader/{0}/{1}/profile/json", gameVersion, loaderVersion)
            Else
                url = String.Format("https://meta.quiltmc.org/v3/versions/loader/{0}/{1}/profile/json", gameVersion, loaderVersion)
            End If

            Dim name As String = If(kind = LoaderKind.Fabric, "fabric-loader", "quilt-loader")
            Dim versionId = name & "-" & loaderVersion & "-" & gameVersion

            report("获取 " & name & " profile ...")
            Dim json = Http.GetString(url)
            Dim versionsDir = Path.Combine(gameDir, "versions", versionId)
            Directory.CreateDirectory(versionsDir)
            File.WriteAllText(Path.Combine(versionsDir, versionId & ".json"), json)

            report("下载 " & name & " 依赖库 ...")
            GameInstaller.Install(versionId, gameDir, report)
        End Sub

        ''' <summary>兼容性检查：该游戏版本是否有可用加载器。</summary>
        Public Shared Function CheckGameVersion(kind As LoaderKind, gameVersion As String) As Boolean
            Try
                Return GetLoaderVersions(kind, gameVersion).Count > 0
            Catch
                Return False
            End Try
        End Function

    End Class

    ''' <summary>Forge 安装（下载官方安装器并用本机 Java 执行 --installClient）。</summary>
    Public Class ForgeInstaller

        Public Class ForgeEntry
            Public Property Build As String      ' 如 47.2.0
            Public Property FullVersion As String ' 如 1.20.1-47.2.0
        End Class

        ''' <summary>获取某游戏版本推荐的 Forge 版本列表（来自 promotions + maven 列表）。</summary>
        Public Shared Function GetVersions(gameVersion As String) As List(Of ForgeEntry)
            Dim result As New List(Of ForgeEntry)
            ' promotions_slim 给出 recommended/latest
            Using doc = Http.GetJson("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json")
                Dim promos = doc.RootElement.GetProperty("promos")
                For Each p In promos.EnumerateObject()
                    If p.Name.StartsWith(gameVersion & "-", StringComparison.OrdinalIgnoreCase) Then
                        Dim suffix = p.Name.Substring(gameVersion.Length + 1)
                        If suffix = "recommended" OrElse suffix = "latest" Then
                            Dim build = p.Value.GetString()
                            Dim fe As New ForgeEntry With {.Build = build, .FullVersion = gameVersion & "-" & build}
                            If Not result.Exists(Function(x) x.FullVersion = fe.FullVersion) Then result.Add(fe)
                        End If
                    End If
                Next
            End Using
            If result.Count = 0 Then
                ' 退路：尝试 maven-metadata（XML，直接文本解析）
                Try
                    Dim xml = Http.GetString("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml")
                    Dim idx = 0
                    While True
                        idx = xml.IndexOf("<version>" & gameVersion & "-", idx, StringComparison.Ordinal)
                        If idx < 0 Then Exit While
                        Dim endIdx = xml.IndexOf("</version>", idx)
                        Dim ver = xml.Substring(idx + 9, endIdx - idx - 9)
                        Dim fe As New ForgeEntry With {.Build = ver.Substring(gameVersion.Length + 1), .FullVersion = ver}
                        If Not result.Exists(Function(x) x.FullVersion = fe.FullVersion) Then result.Add(fe)
                        idx = endIdx
                    End While
                Catch
                End Try
            End If
            Return result
        End Function

        ''' <summary>安装 Forge（运行官方安装器，需要本机 Java）。</summary>
        Public Shared Sub Install(gameVersion As String, build As String, gameDir As String,
                                  javaPath As String, report As Action(Of String))
            Dim fullVersion = gameVersion & "-" & build
            Dim url = String.Format("https://maven.minecraftforge.net/net/minecraftforge/forge/{0}/forge-{0}-installer.jar", fullVersion)
            Dim installerPath = Path.Combine(Path.GetTempPath(), "easyer-forge-installer.jar")

            report("下载 Forge 安装器 ...")
            Http.DownloadFile(url, installerPath)

            report("运行 Forge 官方安装器（可能需要几分钟）...")
            Dim psi As New ProcessStartInfo With {
                .FileName = javaPath,
                .Arguments = "-jar """ & installerPath & """ --installClient """ & gameDir & """",
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True
            }
            Using p = Process.Start(psi)
                Dim outTask = p.StandardOutput.ReadToEndAsync()
                Dim errTask = p.StandardError.ReadToEndAsync()
                p.WaitForExit()
                Dim output = outTask.Result & Environment.NewLine & errTask.Result
                If p.ExitCode <> 0 Then
                    Throw New Exception("Forge 安装失败 (exit " & p.ExitCode & "): " & output.Substring(0, Math.Min(1000, output.Length)))
                End If
            End Using
            report("Forge 安装完成 ✓")
        End Sub

    End Class

End Namespace
