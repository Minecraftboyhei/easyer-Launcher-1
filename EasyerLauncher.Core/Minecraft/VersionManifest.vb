Imports System.Collections.Generic
Imports EasyerLauncher.Core.Net

Namespace Minecraft

    ''' <summary>版本清单（Mojang piston-meta）。</summary>
    Public Class VersionManifest

        Public Const ManifestUrl As String = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"

        Public Class VersionEntry
            Public Property Id As String
            Public Property Type As String      ' release / snapshot / old_beta / old_alpha
            Public Property Url As String
            Public Property ReleaseTime As String

            Public ReadOnly Property TypeCn As String
                Get
                    Select Case Type
                        Case "release" : Return "正式版"
                        Case "snapshot" : Return "快照"
                        Case "old_beta" : Return "Beta"
                        Case "old_alpha" : Return "Alpha"
                        Case Else : Return Type
                    End Select
                End Get
            End Property
        End Class

        ''' <summary>获取全部版本（国内镜像优先，官方兜底）。</summary>
        Public Shared Function GetVersions() As List(Of VersionEntry)
            Dim list As New List(Of VersionEntry)
            Using doc = System.Text.Json.JsonDocument.Parse(Http.GetStringAuto(ManifestUrl))
                For Each v In doc.RootElement.GetProperty("versions").EnumerateArray()
                    list.Add(New VersionEntry With {
                        .Id = v.GetProperty("id").GetString(),
                        .Type = v.GetProperty("type").GetString(),
                        .Url = v.GetProperty("url").GetString(),
                        .ReleaseTime = v.GetProperty("releaseTime").GetString()
                    })
                Next
            End Using
            Return list
        End Function

        ''' <summary>按 id 查找版本。</summary>
        Public Shared Function FindVersion(id As String) As VersionEntry
            For Each v In GetVersions()
                If v.Id.Equals(id, StringComparison.OrdinalIgnoreCase) Then Return v
            Next
            Return Nothing
        End Function

        ''' <summary>获取最新正式版 id。</summary>
        Public Shared Function LatestRelease() As String
            Using doc = System.Text.Json.JsonDocument.Parse(Http.GetStringAuto(ManifestUrl))
                Return doc.RootElement.GetProperty("latest").GetProperty("release").GetString()
            End Using
        End Function

    End Class

End Namespace
