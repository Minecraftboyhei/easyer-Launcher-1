# Easyer Launcher 1

一个用 **C#（界面）+ VB.NET（核心库）** 写的 Minecraft Java 版启动器，Windows 11 风格。

## 怎么跑

```
cd "F:\easyer Launcher 1"
dotnet run --project EasyerLauncher
```

或者直接双击：`EasyerLauncher\bin\Debug\net8.0-windows\EasyerLauncher.exe`

（需要 .NET 8 SDK 才能编译，本机已装）

## 功能

| 功能 | 说明 |
|---|---|
| 版本下载 | 全部 Mojang 官方版本（正式版/快照/Beta/Alpha），带 SHA1 校验与断点补全 |
| 加载器 | Fabric / Quilt（meta API 直装）、Forge（官方安装器，需本机 Java） |
| MOD / 资源 | Modrinth 源搜索下载：MOD、资源包、光影、数据包，自动放入对应目录 |
| 离线登录 | 输入昵即玩，UUID 用 authlib 官方离线算法生成 |
| 正版登录 | Microsoft 设备码流程（需自建 Azure 应用，填 Client ID 到账号页） |
| 多主题 | Win11 深色 / 浅色 / 完全自定义（背景+卡片+文字色）+ 6 种强调色预设 |
| 其他 | Java 自动检测、内存/分辨率设置、直连服务器参数 |

## 项目结构

```
EasyerLauncher.Core\          VB.NET 类库（核心逻辑）
  Minecraft\GameInstaller.vb     版本解析（含 inheritsFrom 继承链）、客户端/库/资源下载
  Minecraft\GameLauncher.vb      JVM 参数组装与启动
  Minecraft\LoaderInstallers.vb  Fabric / Quilt / Forge
  Marketplace\ModrinthClient.vb  Modrinth 搜索与下载
  Auth\Auth.vb                   离线登录 + Microsoft 设备码流程（XBL→XSTS→MC）
  SystemHelpers\JavaLocator.vb   本机 Java 探测（注册表+常见目录+PATH）
EasyerLauncher\               C# WinForms 主程序（Windows 11 风格 UI）
  Theme.cs                       主题引擎 + Win11 圆角/深色标题栏(DWM)
  MainForm.cs                    侧边栏导航主窗体
  Pages\Pages.cs                 主页/下载/加载器/MOD/账号/设置 六个页面
```

## 注意

- 正版登录：去 portal.azure.com 免费注册一个应用（账户类型选"个人"，平台加"移动应用程序"），把 Client ID 填到「账号」页即可用设备码登录。
- Forge 安装会下载官方安装器并用本机 Java 执行 `--installClient`，第一次较慢属正常。
- 游戏默认装在 `%APPDATA%\.easyer`，可在设置页改。
