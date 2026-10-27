Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Minecraft

    ''' <summary>
    ''' Modrinth 整合包（.mrpack）安装器。
    ''' 流程：下载 mrpack → 读 modrinth.index.json → 装原版 + 加载器 → 建实例版本 JSON
    '''      → 按清单下载 mods/config 等文件到实例目录 → 解压 overrides。
    ''' 每个整合包装成独立实例，实例目录就是它的 gameDir，互不干扰。
    ''' </summary>
    Public Class MrpackInstaller

        Public Class PackFile
            Public Property Path As String = ""
            Public Property Url As String = ""
            Public Property Sha1 As String = ""
        End Class

        Public Class PackIndex
            Public Property Name As String = ""
            Public Property VersionId As String = ""
            Public Property GameVersion As String = ""
            Public Property Loader As String = ""          ' fabric / quilt / forge / neoforge / 空 = 原版
            Public Property LoaderVersion As String = ""
            Public Property Files As New List(Of PackFile)
        End Class

        ''' <summary>实例 id（用作 versions 目录名，必须安全）。</summary>
        Public Shared Function InstanceId(slug As String, packVersionId As String) As String
            Dim sb As New StringBuilder("pack-")
            For Each ch In (slug & "-" & packVersionId)
                If Char.IsLetterOrDigit(ch) Then sb.Append(Char.ToLowerInvariant(ch)) Else sb.Append("-"c)
            Next
            Dim s = sb.ToString()
            Do While s.Contains("--")
                s = s.Replace("--", "-")
            Loop
            Return s.TrimEnd("-"c)
        End Function

        ''' <summary>读取 .mrpack 内的 modrinth.index.json。</summary>
        Public Shared Function ReadIndex(packPath As String) As PackIndex
            Dim idx As New PackIndex
            Using zip = ZipFile.OpenRead(packPath)
                Dim entry = zip.GetEntry("modrinth.index.json")
                If entry Is Nothing Then Throw New Exception("不是有效的整合包（缺少 modrinth.index.json）")

                Dim text As String
                Using s = entry.Open()
                    Using sr As New StreamReader(s, Encoding.UTF8)
                        text = sr.ReadToEnd()
                    End Using
                End Using

                Using doc = JsonDocument.Parse(text)
                    Dim root = doc.RootElement
                    Dim el As JsonElement

                    If root.TryGetProperty("name", el) Then idx.Name = el.GetString()
                    If root.TryGetProperty("versionId", el) Then idx.VersionId = el.GetString()

                    Dim deps As JsonElement
                    If root.TryGetProperty("dependencies", deps) Then
                        Dim mcEl As JsonElement
                        If deps.TryGetProperty("minecraft", mcEl) Then idx.GameVersion = mcEl.GetString()
                        Dim v As JsonElement
                        If deps.TryGetProperty("fabric-loader", v) Then
                            idx.Loader = "fabric" : idx.LoaderVersion = v.GetString()
                        ElseIf deps.TryGetProperty("quilt-loader", v) Then
                            idx.Loader = "quilt" : idx.LoaderVersion = v.GetString()
                        ElseIf deps.TryGetProperty("forge", v) Then
                            idx.Loader = "forge" : idx.LoaderVersion = v.GetString()
                        ElseIf deps.TryGetProperty("neoforge", v) Then
                            idx.Loader = "neoforge" : idx.LoaderVersion = v.GetString()
                        End If
                    End If

                    Dim filesEl As JsonElement
                    If root.TryGetProperty("files", filesEl) AndAlso filesEl.ValueKind = JsonValueKind.Array Then
                        For Each f In filesEl.EnumerateArray()
                            Dim pf As New PackFile
                            Dim p As JsonElement
                            If f.TryGetProperty("path", p) Then pf.Path = p.GetString()
                            Dim dl As JsonElement
                            If f.TryGetProperty("downloads", dl) AndAlso dl.ValueKind = JsonValueKind.Array AndAlso dl.GetArrayLength() > 0 Then
                                pf.Url = dl(0).GetString()
                            End If
                            Dim hs As JsonElement
                            If f.TryGetProperty("hashes", hs) Then
                                Dim sha As JsonElement
                                If hs.TryGetProperty("sha1", sha) Then pf.Sha1 = sha.GetString()
                            End If
                            If pf.Path <> "" AndAlso pf.Url <> "" Then idx.Files.Add(pf)
                        Next
                    End If
                End Using
            End Using
            Return idx
        End Function

        ''' <summary>安装整合包为独立实例，返回实例 id。rootDir = 游戏根目录（versions/libraries/assets 所在）。</summary>
        Public Shared Function Install(packUrl As String, slug As String, rootDir As String, javaPath As String,
                                       report As Action(Of String)) As String
            Dim tmp = Path.Combine(Path.GetTempPath(), "easyer-" & Guid.NewGuid().ToString("N") & ".mrpack")
            report("下载整合包 ...")
            Http.DownloadFile(packUrl, tmp, Nothing, Nothing, 600)

            Try
                report("解析整合包清单 ...")
                Dim idx = ReadIndex(tmp)
                If idx.GameVersion = "" Then Throw New Exception("整合包没有声明 Minecraft 版本，无法安装")

                Dim instId = InstanceId(slug, idx.VersionId)
                Dim instDir = Path.Combine(rootDir, "instances", instId)
                Directory.CreateDirectory(instDir)

                ' 1) 原版
                report("准备原版 " & idx.GameVersion & " ...")
                GameInstaller.Install(idx.GameVersion, rootDir, report)

                ' 2) 加载器
                Dim parentId = idx.GameVersion
                If idx.Loader <> "" Then
                    report("安装 " & idx.Loader & " " & idx.LoaderVersion & " ...")
                    parentId = InstallLoader(idx, rootDir, javaPath, report)
                End If

                ' 3) 实例版本 JSON：继承加载器（或原版）
                Dim verDir = Path.Combine(rootDir, "versions", instId)
                Directory.CreateDirectory(verDir)
                Dim meta = "{""id"":""" & instId & """,""inheritsFrom"":""" & parentId &
                           """,""type"":""release"",""libraries"":[]}"
                File.WriteAllText(Path.Combine(verDir, instId & ".json"), meta, New UTF8Encoding(False))
                report("已创建实例版本 " & instId)

                ' 4) 按清单下载文件到实例目录
                Dim i = 0
                Dim failed = 0
                For Each f In idx.Files
                    i += 1
                    Dim dest = Path.Combine(instDir, f.Path.Replace("/"c, Path.DirectorySeparatorChar))
                    If File.Exists(dest) Then Continue For
                    report(String.Format("下载文件 ({0}/{1}): {2}", i, idx.Files.Count, Path.GetFileName(f.Path)))
                    Try
                        Http.DownloadFile(f.Url, dest, If(f.Sha1 = "", Nothing, f.Sha1), Nothing, 180)
                    Catch ex As Exception
                        failed += 1
                        report("跳过 " & f.Path & "（" & ex.Message & "）")
                    End Try
                Next
                If failed > 0 Then report(failed & " 个文件下载失败，游戏里可能缺内容")

                ' 5) overrides / client-overrides
                report("解压 overrides ...")
                ExtractZipDir(tmp, "overrides/", instDir, report)
                ExtractZipDir(tmp, "client-overrides/", instDir, report)

                report("整合包安装完成 ✓")
                Return instId
            Finally
                Try
                    File.Delete(tmp)
                Catch
                End Try
            End Try
        End Function

        ''' <summary>安装加载器，返回可用的父版本 id（扫描 versions 目录新增项，不依赖命名规则）。</summary>
        Private Shared Function InstallLoader(idx As PackIndex, rootDir As String, javaPath As String,
                                             report As Action(Of String)) As String
            Dim before = New HashSet(Of String)(GameInstaller.ListInstalledVersions(rootDir), StringComparer.OrdinalIgnoreCase)

            Select Case idx.Loader.ToLowerInvariant()
                Case "fabric"
                    FabricInstaller.Install(FabricInstaller.LoaderKind.Fabric, idx.GameVersion, idx.LoaderVersion, rootDir, report)
                    Return "fabric-loader-" & idx.LoaderVersion & "-" & idx.GameVersion
                Case "quilt"
                    FabricInstaller.Install(FabricInstaller.LoaderKind.Quilt, idx.GameVersion, idx.LoaderVersion, rootDir, report)
                    Return "quilt-loader-" & idx.LoaderVersion & "-" & idx.GameVersion
                Case "forge"
                    ForgeInstaller.Install(idx.GameVersion, idx.LoaderVersion, rootDir, javaPath, report)
                Case "neoforge"
                    NeoForgeInstaller.Install(idx.GameVersion, idx.LoaderVersion, rootDir, javaPath, report)
                Case Else
                    Return idx.GameVersion
            End Select

            ' Forge / NeoForge 打出来的版本 id 不好预测 → 取新增的那个
            Dim added = GameInstaller.ListInstalledVersions(rootDir).
                        Where(Function(v) Not before.Contains(v)).
                        OrderByDescending(Function(v) v).ToList()
            If added.Count > 0 Then Return added(0)
            Return idx.GameVersion
        End Function

        ''' <summary>把 zip 中某个前缀下的所有条目解到目标目录（去掉前缀）。</summary>
        Private Shared Sub ExtractZipDir(zipPath As String, prefix As String, destDir As String, report As Action(Of String))
            Using zip = ZipFile.OpenRead(zipPath)
                For Each entry In zip.Entries
                    If Not entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then Continue For
                    Dim rel = entry.FullName.Substring(prefix.Length)
                    If rel = "" OrElse entry.FullName.EndsWith("/") Then Continue For
                    Dim dest = Path.Combine(destDir, rel.Replace("/"c, Path.DirectorySeparatorChar))
                    Dim parent = Path.GetDirectoryName(dest)
                    If Not String.IsNullOrEmpty(parent) Then Directory.CreateDirectory(parent)
                    Try
                        entry.ExtractToFile(dest, True)
                    Catch ex As Exception
                        report("解压失败 " & rel & "：" & ex.Message)
                    End Try
                Next
            End Using
        End Sub

    End Class

End Namespace
