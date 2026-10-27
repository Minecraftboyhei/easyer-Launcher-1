Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Marketplace

    ''' <summary>Modrinth 内容源：MOD / 资源包 / 光影 / 数据包 搜索与下载。</summary>
    Public Class ModrinthClient

        ''' <summary>官方接口（境外，部分网络不可达）。</summary>
        Public Const ApiBase As String = "https://api.modrinth.com/v2"

        ''' <summary>可用接口源，按优先顺序：国内镜像（MCIM，秒开）→ 官方兜底。会记住上次成功的那个。</summary>
        Private Shared ReadOnly ApiBases As String() = {
            "https://mod.mcimirror.top/modrinth/v2",
            "https://api.modrinth.com/v2"
        }
        Private Shared _baseIndex As Integer = 0
        Private Shared ReadOnly _baseLock As New Object()

        ''' <summary>当前正在用的接口源（供界面提示）。</summary>
        Public Shared ReadOnly Property ActiveApiBase As String
            Get
                Return ApiBases(_baseIndex)
            End Get
        End Property

        ''' <summary>
        ''' 按源依次请求 JSON：官方 8 秒不通就切镜像，成功的一方会被记住，后续直接走它。
        ''' </summary>
        Private Shared Function GetJsonAuto(pathAndQuery As String) As JsonDocument
            Dim start = _baseIndex
            Dim lastErr As Exception = Nothing
            For attempt = 0 To ApiBases.Length - 1
                Dim idx = (start + attempt) Mod ApiBases.Length
                Dim url = ApiBases(idx) & pathAndQuery
                Try
                    Dim txt = Http.GetString(url, 8, False)   ' 快速失败，不重试，否则切源要等半分钟
                    Dim doc = JsonDocument.Parse(txt)
                    SyncLock _baseLock
                        _baseIndex = idx
                    End SyncLock
                    Return doc
                Catch ex As Exception
                    lastErr = ex
                End Try
            Next
            Throw New Exception("Modrinth 接口连不上（官方和国内镜像都没响应）。" & Environment.NewLine &
                                "请检查网络或代理设置后重试；离线安装不受影响。", lastErr)
        End Function

        Public Enum ContentType
            Mods = 0
            ResourcePack = 1
            Shader = 2
            DataPack = 3
            Modpack = 4
        End Enum

        Public Class SearchResult
            Public Property Slug As String
            Public Property Title As String
            Public Property Description As String
            Public Property Downloads As Long
            Public Property IconUrl As String = ""
            Public Property ProjectType As String
            Public Property ClientSide As String = ""
            Public Property ServerSide As String = ""
            Public Property Versions As New List(Of String)
        End Class

        Public Class VersionFile
            Public Property Id As String
            Public Property VersionNumber As String
            Public Property Name As String
            Public Property FileName As String
            Public Property Url As String
            Public Property Size As Long
            Public Property GameVersions As New List(Of String)
            Public Property Loaders As New List(Of String)
            Public Property DatePublished As String
        End Class

        Public Shared Function TypeFacet(t As ContentType) As String
            Select Case t
                Case ContentType.Mods : Return "project_type:mod"
                Case ContentType.ResourcePack : Return "project_type:resourcepack"
                Case ContentType.Shader : Return "project_type:shader"
                Case ContentType.DataPack : Return "project_type:datapack"
                Case ContentType.Modpack : Return "project_type:modpack"
            End Select
            Return "project_type:mod"
        End Function

        Public Shared Function ContentTypeOf(projectType As String) As ContentType
            Select Case projectType
                Case "resourcepack" : Return ContentType.ResourcePack
                Case "shader" : Return ContentType.Shader
                Case "datapack" : Return ContentType.DataPack
                Case "modpack" : Return ContentType.Modpack
                Case Else : Return ContentType.Mods
            End Select
        End Function

        ''' <summary>搜索。</summary>
        Public Shared Function Search(query As String, t As ContentType,
                                      Optional gameVersion As String = Nothing,
                                      Optional loader As String = Nothing,
                                      Optional limit As Integer = 20) As List(Of SearchResult)
            Dim facets As New List(Of String) From {TypeFacet(t)}
            If Not String.IsNullOrEmpty(gameVersion) Then facets.Add("versions:" & gameVersion)
            If Not String.IsNullOrEmpty(loader) AndAlso t = ContentType.Mods Then facets.Add("categories:" & loader)

            Dim facetsJson = JsonSerializer.Serialize(New List(Of List(Of String)) From {facets})
            Dim path = "/search?limit=" & limit & "&query=" & Uri.EscapeDataString(query) &
                       "&facets=" & Uri.EscapeDataString(facetsJson)

            Dim result As New List(Of SearchResult)
            Using doc = GetJsonAuto(path)
                For Each hit In doc.RootElement.GetProperty("hits").EnumerateArray()
                    Dim el As JsonElement
                    Dim sr As New SearchResult With {
                        .Slug = hit.GetProperty("slug").GetString(),
                        .Title = hit.GetProperty("title").GetString(),
                        .Description = "",
                        .Downloads = hit.GetProperty("downloads").GetInt64(),
                        .ProjectType = hit.GetProperty("project_type").GetString()
                    }
                    If hit.TryGetProperty("description", el) Then sr.Description = el.GetString()
                    If hit.TryGetProperty("icon_url", el) AndAlso el.ValueKind = JsonValueKind.String Then sr.IconUrl = el.GetString()
                    If hit.TryGetProperty("client_side", el) Then sr.ClientSide = el.GetString()
                    If hit.TryGetProperty("server_side", el) Then sr.ServerSide = el.GetString()
                    If hit.TryGetProperty("versions", el) AndAlso el.ValueKind = JsonValueKind.Array Then
                        For Each v In el.EnumerateArray()
                            sr.Versions.Add(v.GetString())
                        Next
                    End If
                    result.Add(sr)
                Next
            End Using
            Return result
        End Function

        ''' <summary>列出某项目的版本文件（可按游戏版本与加载器过滤）。</summary>
        Public Shared Function GetVersions(slug As String,
                                           Optional gameVersion As String = Nothing,
                                           Optional loader As String = Nothing) As List(Of VersionFile)
            Dim url = "/project/" & slug & "/version"
            Dim params As New List(Of String)
            If Not String.IsNullOrEmpty(gameVersion) Then
                params.Add("game_versions=" & Uri.EscapeDataString(JsonSerializer.Serialize(New List(Of String) From {gameVersion})))
            End If
            If Not String.IsNullOrEmpty(loader) Then
                params.Add("loaders=" & Uri.EscapeDataString(JsonSerializer.Serialize(New List(Of String) From {loader})))
            End If
            If params.Count > 0 Then url &= "?" & String.Join("&", params)

            Dim result As New List(Of VersionFile)
            Using doc = GetJsonAuto(url)
                For Each v In doc.RootElement.EnumerateArray()
                    Dim vf As New VersionFile With {
                        .Id = v.GetProperty("id").GetString(),
                        .VersionNumber = v.GetProperty("version_number").GetString(),
                        .Name = "",
                        .DatePublished = v.GetProperty("date_published").GetString()
                    }
                    Dim el As JsonElement
                    If v.TryGetProperty("name", el) Then vf.Name = el.GetString()
                    If v.TryGetProperty("game_versions", el) Then
                        For Each g In el.EnumerateArray() : vf.GameVersions.Add(g.GetString()) : Next
                    End If
                    If v.TryGetProperty("loaders", el) Then
                        For Each g In el.EnumerateArray() : vf.Loaders.Add(g.GetString()) : Next
                    End If
                    If v.TryGetProperty("files", el) AndAlso el.GetArrayLength() > 0 Then
                        Dim f = el(0)
                        vf.FileName = f.GetProperty("filename").GetString()
                        vf.Url = f.GetProperty("url").GetString()
                        vf.Size = f.GetProperty("size").GetInt64()
                        result.Add(vf)
                    End If
                Next
            End Using
            Return result
        End Function

        ''' <summary>下载内容到对应目录（mods / resourcepacks / shaderpacks / datapacks）。整合包请用 MrpackInstaller。</summary>
        Public Shared Sub Download(file As VersionFile, t As ContentType, gameDir As String, report As Action(Of String))
            If t = ContentType.Modpack Then
                Throw New Exception("整合包要用 MrpackInstaller 安装，不能直接放进目录")
            End If
            Dim subDir As String
            Select Case t
                Case ContentType.Mods : subDir = "mods"
                Case ContentType.ResourcePack : subDir = "resourcepacks"
                Case ContentType.Shader : subDir = "shaderpacks"
                Case Else : subDir = "datapacks"
            End Select
            Dim dest = Path.Combine(gameDir, subDir, file.FileName)
            report("下载 " & file.FileName & " ...")
            Http.DownloadFile(file.Url, dest)
            report("已保存到 " & subDir & "/" & file.FileName & " ✓")
        End Sub

        Public Shared Function FormatDownloads(n As Long) As String
            If n >= 1000000 Then Return (n / 1000000.0).ToString("0.#") & "M"
            If n >= 1000 Then Return (n / 1000.0).ToString("0.#") & "K"
            Return n.ToString()
        End Function

        Public Shared Function FormatSize(bytes As Long) As String
            If bytes >= 1048576 Then Return (bytes / 1048576.0).ToString("0.#") & " MB"
            If bytes >= 1024 Then Return (bytes / 1024.0).ToString("0.#") & " KB"
            Return bytes & " B"
        End Function

    End Class

End Namespace
