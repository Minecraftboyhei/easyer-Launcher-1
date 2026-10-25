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

        ''' <summary>获取某游戏版本推荐的 Forge 版本列表（官方 promos 优先，BMCLAPI 兜底）。</summary>
        Public Shared Function GetVersions(gameVersion As String) As List(Of ForgeEntry)
            Dim result As New List(Of ForgeEntry)
            ' promotions_slim 给出 recommended/latest（官方 → BMCLAPI 双源）
            For Each promoUrl In {"https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json",
                                  "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/promotions_slim.json"}
                Try
                    Using doc = Http.GetJson(promoUrl)
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
                    If result.Count > 0 Then Return result
                Catch
                End Try
            Next
            If result.Count = 0 Then
                ' 退路：maven-metadata（官方 → BMCLAPI 双源，XML 文本解析）
                For Each metaUrl In {"https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml",
                                     "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/maven-metadata.xml"}
                    Try
                        Dim xml = Http.GetString(metaUrl)
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
                        If result.Count > 0 Then Return result
                    Catch
                    End Try
                Next
            End If
            Return result
        End Function

        ''' <summary>安装 Forge（运行官方安装器，需要本机 Java）。安装器下载官方优先、BMCLAPI 兜底。</summary>
        Public Shared Sub Install(gameVersion As String, build As String, gameDir As String,
                                  javaPath As String, report As Action(Of String))
            Dim fullVersion = gameVersion & "-" & build
            Dim installerPath = Path.Combine(Path.GetTempPath(), "easyer-forge-installer.jar")
            Dim urls = {
                String.Format("https://maven.minecraftforge.net/net/minecraftforge/forge/{0}/forge-{0}-installer.jar", fullVersion),
                String.Format("https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{0}/forge-{0}-installer.jar", fullVersion)
            }

            Dim lastEx As Exception = Nothing
            Dim ok = False
            For Each url In urls
                Try
                    report("下载 Forge 安装器 ...")
                    Http.DownloadFile(url, installerPath)
                    ok = True
                    Exit For
                Catch ex As Exception
                    lastEx = ex
                End Try
            Next
            If Not ok Then
                Throw New Exception("Forge 安装器下载失败（官方与 BMCLAPI 都不可达）：" &
                                    If(lastEx IsNot Nothing, lastEx.Message, ""))
            End If

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

    ''' <summary>NeoForge 安装（Maven 元数据列版本 + 官方安装器 --installClient）。</summary>
    Public Class NeoForgeInstaller

        ''' <summary>NeoForge 21.x 时代版本号与 MC 版本的对应规则：1.20.1 特殊（47.1.x），其余为 MC 版本去掉"1."前缀。</summary>
        Public Shared Function VersionPrefix(gameVersion As String) As String
            If gameVersion = "1.20.1" Then Return "47.1"
            If gameVersion.StartsWith("1.") AndAlso gameVersion.Count(Function(c) c = "."c) >= 2 Then
                Return gameVersion.Substring(2)
            End If
            Return Nothing
        End Function

        ''' <summary>该 MC 版本是否支持 NeoForge（1.20.1 及以上）。</summary>
        Public Shared Function IsSupported(gameVersion As String) As Boolean
            Try
                Dim parts = gameVersion.Split("."c)
                If parts.Length >= 2 AndAlso parts(0) = "1" AndAlso Integer.Parse(parts(1)) >= 20 Then Return True
            Catch
            End Try
            Return False
        End Function

        ''' <summary>获取某游戏版本的 NeoForge 版本列表（新→旧）。BMCLAPI 优先，官方 maven 兜底。</summary>
        Public Shared Function GetVersions(gameVersion As String) As List(Of String)
            Dim prefix = VersionPrefix(gameVersion)
            If prefix Is Nothing Then Return New List(Of String)

            ' 1.20.1 时代的 NeoForge 挂在 net/neoforged/forge 坐标下，其余在 net/neoforged/neoforge
            Dim artifact = If(gameVersion = "1.20.1", "forge", "neoforge")
            Dim urls = {
                "https://bmclapi2.bangbang93.com/maven/net/neoforged/" & artifact & "/maven-metadata.xml",
                "https://maven.neoforged.net/releases/net/neoforged/" & artifact & "/maven-metadata.xml"
            }

            For Each url In urls
                Try
                    Dim xml = Http.GetString(url)
                    Dim result = ParseMetadata(xml, prefix)
                    If result.Count > 0 Then Return result
                Catch
                    ' 换下一个源
                End Try
            Next
            Return New List(Of String)
        End Function

        Private Shared Function ParseMetadata(xml As String, prefix As String) As List(Of String)
            Dim result As New List(Of String)
            Dim idx = 0
            While True
                idx = xml.IndexOf("<version>" & prefix & ".", idx, StringComparison.Ordinal)
                If idx < 0 Then Exit While
                Dim endIdx = xml.IndexOf("</version>", idx)
                Dim ver = xml.Substring(idx + 9, endIdx - idx - 9)
                If Not result.Contains(ver) Then result.Add(ver)
                idx = endIdx
            End While
            ' 新→旧排序（数字比较，容忍非数字尾巴）
            result.Sort(Function(a, b)
                            Dim pa = a.Split("."c)
                            Dim pb = b.Split("."c)
                            For i As Integer = 0 To 2
                                Dim ia As Integer = 0
                                Dim ib As Integer = 0
                                Integer.TryParse(If(i < pa.Length, pa(i), "0"), ia)
                                Integer.TryParse(If(i < pb.Length, pb(i), "0"), ib)
                                If ia <> ib Then Return ib.CompareTo(ia)
                            Next
                            Return 0
                        End Function)
            Return result
        End Function

        ''' <summary>安装 NeoForge（运行官方安装器，需要本机 Java）。下载走 BMCLAPI 优先、官方兜底。</summary>
        Public Shared Sub Install(gameVersion As String, neoVersion As String, gameDir As String,
                                  javaPath As String, report As Action(Of String))
            Dim artifact = If(gameVersion = "1.20.1", "forge", "neoforge")
            Dim installerName = If(gameVersion = "1.20.1", "forge", "neoforge") & "-" & neoVersion & "-installer.jar"
            Dim urls = {
                "https://bmclapi2.bangbang93.com/maven/net/neoforged/" & artifact & "/" & neoVersion & "/" & installerName,
                "https://maven.neoforged.net/releases/net/neoforged/" & artifact & "/" & neoVersion & "/" & installerName
            }
            Dim installerPath = Path.Combine(Path.GetTempPath(), "easyer-neoforge-installer.jar")

            Dim lastEx As Exception = Nothing
            Dim downloaded = False
            For Each url In urls
                Try
                    report("下载 NeoForge 安装器 ...")
                    Http.DownloadFile(url, installerPath)
                    downloaded = True
                    Exit For
                Catch ex As Exception
                    lastEx = ex
                End Try
            Next
            If Not downloaded Then
                Throw New Exception("NeoForge 安装器下载失败（BMCLAPI 与官方源都不可达）：" &
                                    If(lastEx IsNot Nothing, lastEx.Message, ""))
            End If

            report("运行 NeoForge 官方安装器（可能需要几分钟）...")
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
                    Throw New Exception("NeoForge 安装失败 (exit " & p.ExitCode & "): " & output.Substring(0, Math.Min(1000, output.Length)))
                End If
            End Using
            report("NeoForge 安装完成 ✓")
        End Sub

    End Class

End Namespace
