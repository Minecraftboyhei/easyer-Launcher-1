Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports Microsoft.Win32

Namespace SystemHelpers

    ''' <summary>本机 Java 探测。</summary>
    Public Class JavaLocator

        Public Class JavaInstall
            Public Property JavaExe As String
            Public Property VersionString As String = ""

            Public ReadOnly Property DisplayName As String
                Get
                    Return VersionString & "  —  " & JavaExe
                End Get
            End Property
        End Class

        Public Shared Function FindAll() As List(Of JavaInstall)
            Dim found As New List(Of JavaInstall)
            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            ' 1) PATH 里的 java
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = "java",
                    .Arguments = "-version",
                    .UseShellExecute = False,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True
                }
                Using p = Process.Start(psi)
                    Dim ver = p.StandardError.ReadToEnd()
                    p.WaitForExit()
                    Dim exe = WhereJava()
                    If exe IsNot Nothing AndAlso seen.Add(exe) Then
                        found.Add(New JavaInstall With {.JavaExe = exe, .VersionString = FirstLine(ver)})
                    End If
                End Using
            Catch
            End Try

            ' 2) 注册表 JavaSoft
            Try
                Dim keys = {"SOFTWARE\JavaSoft\JDK", "SOFTWARE\JavaSoft\Java Runtime Environment", "SOFTWARE\JavaSoft\JRE"}
                For Each keyName In keys
                    Using key = Registry.LocalMachine.OpenSubKey(keyName)
                        If key Is Nothing Then Continue For
                        For Each verName In key.GetSubKeyNames()
                            Using subKey = key.OpenSubKey(verName)
                                If subKey Is Nothing Then Continue For
                                Dim home = TryCast(subKey.GetValue("JavaHome"), String)
                                If home IsNot Nothing Then AddIfValid(found, seen, Path.Combine(home, "bin", "java.exe"))
                            End Using
                        Next
                    End Using
                Next
            Catch
            End Try

            ' 3) 常见目录扫描
            Dim roots = {
                "C:\Program Files\Java", "C:\Program Files\Eclipse Adoptium",
                "C:\Program Files\Microsoft\jdk*", "C:\Program Files\Zulu",
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.easyer\runtimes"
            }
            For Each root In roots
                Try
                    If root.Contains("*") Then
                        Dim dirPart = IO.Path.GetDirectoryName(root)
                        If Not IO.Directory.Exists(dirPart) Then Continue For
                        For Each d In IO.Directory.GetDirectories(dirPart, IO.Path.GetFileName(root))
                            AddIfValid(found, seen, IO.Path.Combine(d, "bin", "java.exe"))
                        Next
                    ElseIf IO.Directory.Exists(root) Then
                        For Each d In IO.Directory.GetDirectories(root)
                            AddIfValid(found, seen, IO.Path.Combine(d, "bin", "java.exe"))
                        Next
                    End If
                Catch
                End Try
            Next

            Return found
        End Function

        Private Shared Sub AddIfValid(found As List(Of JavaInstall), seen As HashSet(Of String), exe As String)
            If exe IsNot Nothing AndAlso IO.File.Exists(exe) AndAlso seen.Add(exe) Then
                found.Add(New JavaInstall With {.JavaExe = exe, .VersionString = ProbeVersion(exe)})
            End If
        End Sub

        Private Shared Function WhereJava() As String
            Dim pathEnv = Environment.GetEnvironmentVariable("PATH")
            For Each dirPath In pathEnv.Split(";"c)
                If dirPath.Trim() = "" Then Continue For
                Try
                    Dim candidate = IO.Path.Combine(dirPath.Trim(), "java.exe")
                    If IO.File.Exists(candidate) Then
                        Return IO.Path.GetFullPath(candidate)
                    End If
                Catch
                End Try
            Next
            Return Nothing
        End Function

        Private Shared Function ProbeVersion(exe As String) As String
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exe,
                    .Arguments = "-version",
                    .UseShellExecute = False,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True
                }
                Using p = Process.Start(psi)
                    Dim outp = p.StandardError.ReadToEnd()
                    p.WaitForExit(5000)
                    Return FirstLine(outp)
                End Using
            Catch
                Return "(未知版本)"
            End Try
        End Function

        Private Shared Function FirstLine(s As String) As String
            If String.IsNullOrEmpty(s) Then Return ""
            Dim idx = s.IndexOfAny({ControlChars.Lf, ControlChars.Cr})
            If idx > 0 Then Return s.Substring(0, idx).Trim()
            Return s.Trim()
        End Function

    End Class

End Namespace
