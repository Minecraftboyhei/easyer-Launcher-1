Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports EasyerLauncher.Core.Net

Namespace Marketplace

    ''' <summary>Modrinth 内容源：MOD / 资源包 / 光影 / 数据包 搜索与下载。</summary>
    Public Class ModrinthClient

        Public Const ApiBase As String = "https://api.modrinth.com/v2"

        Public Enum ContentType
            Mods = 0
            ResourcePack = 1
            Shader = 2
            DataPack = 3
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
            End Select
            Return "project_type:mod"
        End Function

        Public Shared Function ContentTypeOf(projectType As String) As ContentType
            Select Case projectType
                Case "resourcepack" : Return ContentType.ResourcePack
                Case "shader" : Return ContentType.Shader
                Case "datapack" : Return ContentType.DataPack
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
            Dim url = ApiBase & "/search?limit=" & limit & "&query=" & Uri.EscapeDataString(query) &
                      "&facets=" & Uri.EscapeDataString(facetsJson)

            Dim result As New List(Of SearchResult)
            Using doc = Http.GetJson(url)
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
            Dim url = ApiBase & "/project/" & slug & "/version"
            Dim params As New List(Of String)
            If Not String.IsNullOrEmpty(gameVersion) Then
                params.Add("game_versions=" & Uri.EscapeDataString(JsonSerializer.Serialize(New List(Of String) From {gameVersion})))
            End If
            If Not String.IsNullOrEmpty(loader) Then
                params.Add("loaders=" & Uri.EscapeDataString(JsonSerializer.Serialize(New List(Of String) From {loader})))
            End If
            If params.Count > 0 Then url &= "?" & String.Join("&", params)

            Dim result As New List(Of VersionFile)
            Using doc = Http.GetJson(url)
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

        ''' <summary>下载内容到对应目录（mods / resourcepacks / shaderpacks / datapacks）。</summary>
        Public Shared Sub Download(file As VersionFile, t As ContentType, gameDir As String, report As Action(Of String))
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
