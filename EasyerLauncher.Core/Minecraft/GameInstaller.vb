Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Minecraft

    ''' <summary>
    ''' 已解析的版本信息：合并 inheritsFrom 继承链后的库列表、主类、参数。
    ''' </summary>
    Public Class ResolvedVersion
        Public Property Id As String
        Public Property MainClass As String
        Public Property MinecraftArguments As String = ""   ' 旧版参数
        Public Property GameArgs As New List(Of String)     ' 新版 arguments.game
        Public Property JvmArgs As New List(Of String)      ' 新版 arguments.jvm
        Public Property Libraries As New List(Of LibraryEntry)
        Public Property AssetIndexId As String = ""
        Public Property AssetIndexUrl As String = ""
        Public Property ClientJarPath As String = ""
        Public Property ClientJarUrl As String = ""
        Public Property ClientJarSha1 As String = ""
        Public Property LoggingFileUrl As String = ""
        Public Property LoggingFilePath As String = ""
        Public Property LoggingArgument As String = ""
        Public Property NativesDir As String = ""
    End Class

    Public Class LibraryEntry
        Public Property Name As String
        Public Property Url As String          ' 下载根（libraries.minecraft.net/），也可能是完整文件 URL
        Public Property Path As String         ' 相对路径
        Public Property Sha1 As String = ""
        Public Property IsNative As Boolean = False

        ''' <summary>实际下载地址：Url 已是完整文件地址就直接用，否则拼上相对路径。</summary>
        Public ReadOnly Property DownloadUrl As String
            Get
                If String.IsNullOrEmpty(Url) Then Return Path
                If Url.EndsWith(Path, StringComparison.OrdinalIgnoreCase) Then Return Url
                Return Url & Path
            End Get
        End Property
    End Class

    ''' <summary>版本 JSON 读取与继承链解析。</summary>
    Public Class VersionResolver

        ''' <summary>读取并解析一个版本（含 inheritsFrom 合并）。</summary>
        Public Shared Function Resolve(versionId As String, versionsDir As String) As ResolvedVersion
            Dim jsonPath = Path.Combine(versionsDir, versionId, versionId & ".json")
            If Not File.Exists(jsonPath) Then
                Dim entry = VersionManifest.FindVersion(versionId)
                If entry Is Nothing Then Throw New Exception("找不到版本: " & versionId)
                Directory.CreateDirectory(Path.Combine(versionsDir, versionId))
                Http.DownloadFileAuto(entry.Url, jsonPath)
            End If
            Dim doc = JsonDocument.Parse(File.ReadAllText(jsonPath))
            Dim rv = ResolveFrom(doc.RootElement, versionsDir, versionId)
            doc.Dispose()
            Return rv
        End Function

        Private Shared Function ResolveFrom(el As JsonElement, versionsDir As String, fallbackId As String) As ResolvedVersion
            Dim rv As New ResolvedVersion
            Dim t As JsonElement

            If el.TryGetProperty("id", t) Then
                rv.Id = t.GetString()
            Else
                rv.Id = fallbackId
            End If

            If el.TryGetProperty("inheritsFrom", t) Then
                ' ===== 有父版本：递归合并 =====
                Dim parentId = t.GetString()
                Dim parent = Resolve(parentId, versionsDir)

                Dim merged As New List(Of LibraryEntry)
                Dim childLibs = ParseLibraries(el)
                For Each libEntry In childLibs
                    Dim replaced = False
                    For i = 0 To parent.Libraries.Count - 1
                        If SameGroupArtifact(parent.Libraries(i).Name, libEntry.Name) Then
                            parent.Libraries(i) = libEntry
                            replaced = True
                            Exit For
                        End If
                    Next
                    If Not replaced Then merged.Add(libEntry)
                Next
                merged.AddRange(parent.Libraries)
                rv.Libraries = merged

                If el.TryGetProperty("mainClass", t) Then
                    rv.MainClass = t.GetString()
                Else
                    rv.MainClass = parent.MainClass
                End If

                If el.TryGetProperty("minecraftArguments", t) Then
                    rv.MinecraftArguments = t.GetString() & " " & parent.MinecraftArguments
                Else
                    rv.MinecraftArguments = parent.MinecraftArguments
                End If

                rv.JvmArgs = New List(Of String)(parent.JvmArgs)
                rv.GameArgs = New List(Of String)(parent.GameArgs)
                If el.TryGetProperty("arguments", t) Then
                    Dim g As JsonElement
                    If t.TryGetProperty("game", g) Then
                        For Each item In g.EnumerateArray()
                            If item.ValueKind = JsonValueKind.String Then rv.GameArgs.Add(item.GetString())
                        Next
                    End If
                End If

                rv.AssetIndexId = parent.AssetIndexId
                rv.AssetIndexUrl = parent.AssetIndexUrl
                rv.ClientJarPath = parent.ClientJarPath
                rv.ClientJarUrl = parent.ClientJarUrl
                rv.ClientJarSha1 = parent.ClientJarSha1
                rv.LoggingFileUrl = parent.LoggingFileUrl
                rv.LoggingFilePath = parent.LoggingFilePath
                rv.LoggingArgument = parent.LoggingArgument

                If el.TryGetProperty("assetIndex", t) Then
                    rv.AssetIndexId = t.GetProperty("id").GetString()
                    rv.AssetIndexUrl = t.GetProperty("url").GetString()
                End If
            Else
                ' ===== 顶层版本 =====
                rv.Libraries = ParseLibraries(el)
                rv.MainClass = el.GetProperty("mainClass").GetString()

                If el.TryGetProperty("minecraftArguments", t) Then rv.MinecraftArguments = t.GetString()

                If el.TryGetProperty("arguments", t) Then
                    Dim subEl As JsonElement
                    If t.TryGetProperty("game", subEl) Then
                        For Each item In subEl.EnumerateArray()
                            If item.ValueKind = JsonValueKind.String Then rv.GameArgs.Add(item.GetString())
                        Next
                    End If
                    If t.TryGetProperty("jvm", subEl) Then
                        For Each item In subEl.EnumerateArray()
                            If item.ValueKind = JsonValueKind.String Then rv.JvmArgs.Add(item.GetString())
                        Next
                    End If
                End If

                If el.TryGetProperty("assetIndex", t) Then
                    rv.AssetIndexId = t.GetProperty("id").GetString()
                    rv.AssetIndexUrl = t.GetProperty("url").GetString()
                End If

                If el.TryGetProperty("downloads", t) Then
                    Dim cl As JsonElement
                    If t.TryGetProperty("client", cl) Then
                        rv.ClientJarPath = Path.Combine(versionsDir, rv.Id, rv.Id & ".jar")
                        rv.ClientJarUrl = cl.GetProperty("url").GetString()
                        If cl.TryGetProperty("sha1", t) Then rv.ClientJarSha1 = t.GetString()
                    End If
                End If

                If el.TryGetProperty("logging", t) Then
                    Dim logC As JsonElement
                    If t.TryGetProperty("client", logC) Then
                        Dim f = logC.GetProperty("file")
                        rv.LoggingFileUrl = f.GetProperty("url").GetString()
                        rv.LoggingFilePath = Path.Combine("logconfigs", f.GetProperty("id").GetString())
                        rv.LoggingArgument = logC.GetProperty("argument").GetString()
                    End If
                End If
            End If

            Return rv
        End Function

        Private Shared Function SameGroupArtifact(a As String, b As String) As Boolean
            If a Is Nothing OrElse b Is Nothing Then Return False
            Dim pa = a.Split(":"c)
            Dim pb = b.Split(":"c)
            Return pa.Length >= 2 AndAlso pb.Length >= 2 AndAlso pa(0) = pb(0) AndAlso pa(1) = pb(1)
        End Function

        ''' <summary>评估版本 JSON 里的 rules。</summary>
        Private Shared Function RuleAllows(rules As JsonElement) As Boolean
            Dim result As Boolean = False
            Dim t As JsonElement, t2 As JsonElement
            For Each r In rules.EnumerateArray()
                If r.TryGetProperty("os", t) Then
                    Dim allow As Boolean = True
                    If t.TryGetProperty("name", t2) Then
                        allow = (t2.GetString() = "windows")
                    End If
                    If allow AndAlso t.TryGetProperty("arch", t2) Then
                        allow = (t2.GetString() = If(Environment.Is64BitOperatingSystem, "x64", "x86"))
                    End If
                    If r.TryGetProperty("action", t2) Then
                        If t2.GetString() = "allow" AndAlso allow Then result = True
                        If t2.GetString() = "disallow" AndAlso allow Then result = False
                    End If
                ElseIf r.TryGetProperty("action", t) Then
                    If t.GetString() = "allow" Then result = True
                End If
            Next
            Return result
        End Function

        Private Shared Function ParseLibraries(el As JsonElement) As List(Of LibraryEntry)
            Dim libs As New List(Of LibraryEntry)
            Dim lsEl As JsonElement
            If Not el.TryGetProperty("libraries", lsEl) Then Return libs

            For Each l In lsEl.EnumerateArray()
                Dim t As JsonElement, t2 As JsonElement
                If l.TryGetProperty("rules", t) Then
                    If Not RuleAllows(t) Then Continue For
                End If
                If Not l.TryGetProperty("name", t) Then Continue For
                Dim name = t.GetString()

                If l.TryGetProperty("downloads", t) Then
                    Dim artEl As JsonElement, clsEl As JsonElement
                    If t.TryGetProperty("artifact", artEl) Then
                        Dim le As New LibraryEntry With {
                            .Name = name,
                            .Url = artEl.GetProperty("url").GetString(),
                            .Path = artEl.GetProperty("path").GetString(),
                            .Sha1 = ""
                        }
                        If artEl.TryGetProperty("sha1", t2) Then le.Sha1 = t2.GetString()
                        libs.Add(le)
                    End If
                    If t.TryGetProperty("classifiers", clsEl) Then
                        Dim nwEl As JsonElement
                        If clsEl.TryGetProperty("natives-windows", nwEl) Then
                            Dim nl As New LibraryEntry With {
                                .Name = name & ":natives-windows",
                                .Url = nwEl.GetProperty("url").GetString(),
                                .Path = nwEl.GetProperty("path").GetString(),
                                .Sha1 = "",
                                .IsNative = True
                            }
                            If nwEl.TryGetProperty("sha1", t2) Then nl.Sha1 = t2.GetString()
                            libs.Add(nl)
                        End If
                    End If
                Else
                    ' 无 downloads（Fabric/Quilt 等 profile）：按 maven 布局推算
                    Dim le2 As New LibraryEntry With {.Name = name, .Url = "https://libraries.minecraft.net/"}
                    If l.TryGetProperty("url", t) Then le2.Url = t.GetString()
                    If Not le2.Url.EndsWith("/") Then le2.Url &= "/"
                    le2.Path = MavenToPath(name)
                    libs.Add(le2)
                End If
            Next
            Return libs
        End Function

        ''' <summary>"group:artifact:ver[:classifier]" → maven 相对路径。</summary>
        Public Shared Function MavenToPath(name As String) As String
            Dim parts = name.Split(":"c)
            Dim groupPath = parts(0).Replace("."c, "/"c)
            Dim artifact = parts(1)
            Dim ver = parts(2)
            Dim baseName = artifact & "-" & ver
            If parts.Length >= 4 Then baseName &= "-" & parts(3)
            Return groupPath & "/" & artifact & "/" & ver & "/" & baseName & ".jar"
        End Function

    End Class

    ''' <summary>游戏文件安装器：客户端 jar、库、natives、资源索引与资源文件。</summary>
    Public Class GameInstaller

        ''' <summary>安装（补全）一个版本的全部文件。</summary>
        Public Shared Sub Install(versionId As String, gameDir As String, report As Action(Of String))
            Dim versionsDir = Path.Combine(gameDir, "versions")
            Directory.CreateDirectory(versionsDir)

            report("解析版本 " & versionId & " ...")
            Dim rv = VersionResolver.Resolve(versionId, versionsDir)

            ' 1) 客户端 jar（继承链最顶层提供）
            If Not String.IsNullOrEmpty(rv.ClientJarUrl) AndAlso Not File.Exists(rv.ClientJarPath) Then
                report("下载客户端 jar ...")
                Http.DownloadFileAuto(rv.ClientJarUrl, rv.ClientJarPath, If(rv.ClientJarSha1 = "", Nothing, rv.ClientJarSha1))
            End If

            ' 2) 库文件
            Dim libsDir = Path.Combine(gameDir, "libraries")
            Dim total = rv.Libraries.Count
            Dim i = 0
            For Each libEntry In rv.Libraries
                i += 1
                Dim dest = Path.Combine(libsDir, libEntry.Path.Replace("/"c, Path.DirectorySeparatorChar))
                Dim need = Not File.Exists(dest)
                If need AndAlso libEntry.Sha1 <> "" AndAlso File.Exists(dest) Then
                    need = Not Http.VerifySha1(dest, libEntry.Sha1)
                End If
                If need Then
                    report(String.Format("下载库 ({0}/{1}): {2}", i, total, libEntry.Name))
                    Http.DownloadFileAuto(libEntry.DownloadUrl, dest, If(libEntry.Sha1 = "", Nothing, libEntry.Sha1))
                End If
            Next

            ' 3) 解压 natives
            rv.NativesDir = Path.Combine(versionsDir, rv.Id, "natives")
            Directory.CreateDirectory(rv.NativesDir)
            For Each libEntry In rv.Libraries
                If libEntry.IsNative Then
                    Dim jar = Path.Combine(libsDir, libEntry.Path.Replace("/"c, Path.DirectorySeparatorChar))
                    If File.Exists(jar) Then ExtractNatives(jar, rv.NativesDir)
                End If
            Next

            ' 4) 资源索引 + 资源文件
            If rv.AssetIndexId <> "" AndAlso rv.AssetIndexUrl <> "" Then
                Dim idxDir = Path.Combine(gameDir, "assets", "indexes")
                Directory.CreateDirectory(idxDir)
                Dim idxPath = Path.Combine(idxDir, rv.AssetIndexId & ".json")
                If Not File.Exists(idxPath) Then
                    report("下载资源索引 ...")
                    Http.DownloadFileAuto(rv.AssetIndexUrl, idxPath)
                End If
                report("检查资源文件 ...")
                DownloadAssets(idxPath, gameDir, report)
            End If

            ' 5) 日志配置
            If rv.LoggingFileUrl <> "" Then
                Dim logPath = Path.Combine(gameDir, rv.LoggingFilePath.Replace("/"c, Path.DirectorySeparatorChar))
                If Not File.Exists(logPath) Then
                    report("下载日志配置 ...")
                    Http.DownloadFileAuto(rv.LoggingFileUrl, logPath)
                End If
            End If

            report("安装完成 ✓")
        End Sub

        Private Shared Sub DownloadAssets(indexPath As String, gameDir As String, report As Action(Of String))
            Dim objectsDir = Path.Combine(gameDir, "assets", "objects")
            Directory.CreateDirectory(objectsDir)
            Dim missing As New List(Of KeyValuePair(Of String, String))
            Using doc = JsonDocument.Parse(File.ReadAllText(indexPath))
                For Each prop In doc.RootElement.GetProperty("objects").EnumerateObject()
                    Dim hash = prop.Value.GetProperty("hash").GetString()
                    Dim dest = Path.Combine(objectsDir, hash.Substring(0, 2), hash)
                    If Not File.Exists(dest) Then
                        missing.Add(New KeyValuePair(Of String, String)(hash, dest))
                    End If
                Next
            End Using
            If missing.Count = 0 Then Return
            Dim done = 0
            Dim lockObj As New Object()
            Dim failed As New List(Of KeyValuePair(Of String, String))
            System.Threading.Tasks.Parallel.ForEach(missing,
                Sub(item)
                    Dim url = "https://resources.download.minecraft.net/" & item.Key.Substring(0, 2) & "/" & item.Key
                    Try
                        ' 小文件：30 秒超时，镜像优先；挂起不再拖死整批
                        Http.DownloadFileAuto(url, item.Value, Nothing, Nothing, 30)
                    Catch
                        SyncLock lockObj
                            failed.Add(item)
                        End SyncLock
                    End Try
                    SyncLock lockObj
                        done += 1
                        If done Mod 50 = 0 OrElse done = missing.Count Then
                            report(String.Format("下载资源 {0}/{1}", done, missing.Count))
                        End If
                    End SyncLock
                End Sub)

            ' 失败的重试一轮（直接走官方源，60 秒超时）
            If failed.Count > 0 Then
                report(String.Format("重试 {0} 个未完成的资源 ...", failed.Count))
                System.Threading.Tasks.Parallel.ForEach(failed,
                    Sub(item)
                        Dim url = "https://resources.download.minecraft.net/" & item.Key.Substring(0, 2) & "/" & item.Key
                        Try
                            Http.DownloadFile(url, item.Value, Nothing, Nothing, 60)
                        Catch
                        End Try
                    End Sub)
            End If
        End Sub

        Private Shared Sub ExtractNatives(jarPath As String, nativesDir As String)
            Using zip As ZipArchive = ZipFile.OpenRead(jarPath)
                For Each entry In zip.Entries
                    If entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) AndAlso
                       Not entry.FullName.Contains("META-INF") Then
                        Dim dest = Path.Combine(nativesDir, entry.Name)
                        entry.ExtractToFile(dest, True)
                    End If
                Next
            End Using
        End Sub

        ''' <summary>列出本地已安装版本。</summary>
        Public Shared Function ListInstalledVersions(gameDir As String) As List(Of String)
            Dim result As New List(Of String)
            Dim versionsDir = Path.Combine(gameDir, "versions")
            If Not Directory.Exists(versionsDir) Then Return result
            For Each d In Directory.GetDirectories(versionsDir)
                Dim id = Path.GetFileName(d)
                If File.Exists(Path.Combine(d, id & ".json")) Then result.Add(id)
            Next
            result.Sort()
            Return result
        End Function

    End Class

End Namespace
