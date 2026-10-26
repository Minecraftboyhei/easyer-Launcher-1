# Easyer Launcher 1

一个用 **C#（界面）+ VB.NET（核心库）** 写的 Minecraft Java 版启动器，Windows 11 / PCL2 风格。
**下载即用，不用装 .NET、不用填任何配置。**

## 下载

到 [Releases](https://github.com/Minecraftboyhei/easyer-Launcher-1/releases) 下最新的 `EasyerLauncher-win-x64.zip`，
解压后双击 `EasyerLauncher.exe` 即可（自带运行时，Win10/11 x64 通用）。

## 登录方式

| 方式 | 说明 | 现在能用吗 |
|---|---|---|
| 离线登录 | 输入昵称直接玩，UUID 用 authlib 官方离线算法 | ✅ |
| LittleSkin 外置登录 | 免微软账号，注册 [littleskin.cn](https://littleskin.cn) 建角色即可；自动下载并挂载 authlib-injector | ✅ |
| Microsoft 正版登录 | 登录应用已内置，账号页点「浏览器登录」即可，**无需填 Client ID** | ⏳ 见下 |

> **关于正版登录**：Mojang 从 2023 年起对第三方启动器实行 **Client ID 白名单**，自建应用必须通过
> [Java Edition API 审核](https://aka.ms/mce-reviewappid)才能调用 `login_with_xbox`。
> 本项目的应用已提交审核；审核通过前，正版登录会在最后一步被 Mojang 拦下（微软账号与 Xbox 认证本身都是通过的）。
> 等审核期间请用离线 / LittleSkin 登录，游戏功能完全一致。

## 功能

| 功能 | 说明 |
|---|---|
| 版本下载 | 全部 Mojang 官方版本（正式版/快照/Beta/Alpha），带 SHA1 校验；安装时弹窗直接选加载器 |
| 加载器 | Fabric / Quilt（meta API 直装）、Forge / NeoForge（官方安装器 + BMCLAPI 镜像双源，需本机 Java） |
| MOD / 资源 | Modrinth 源搜索下载：MOD、资源包、光影、数据包，图标卡片网格，自动放入对应目录 |
| 多主题 | Win11 深色 / 浅色 / 完全自定义（背景+卡片+文字色）+ 6 种强调色预设 |
| 国内加速 | 版本/库/资源/加载器全部走 BMCLAPI 镜像优先、官方源兜底 |
| 体验 | 页面切换动画、悬停渐变、图形化版本/内容卡片、加载器版本列表选择 |
| 其他 | Java 自动检测、内存/分辨率设置、直连服务器参数 |

## 从源码构建

```
dotnet build EasyerLauncher.sln
dotnet run --project EasyerLauncher
```

发布绿色单文件版：

```
dotnet publish EasyerLauncher/EasyerLauncher.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -o dist/win-x64
```

## 项目结构

```
EasyerLauncher.Core\          VB.NET 类库（核心逻辑）
  Minecraft\GameInstaller.vb     版本解析（含 inheritsFrom 继承链）、客户端/库/资源下载
  Minecraft\GameLauncher.vb      JVM 参数组装与启动
  Minecraft\LoaderInstallers.vb  Fabric / Quilt / Forge / NeoForge（BMCLAPI 双源）
  Marketplace\ModrinthClient.vb  Modrinth 搜索与下载
  Auth\Auth.vb                   离线登录 + Microsoft 授权码/设备码流程（XBL→XSTS→MC）
  Auth\LoopbackLogin.vb          浏览器授权码登录（localhost 回环回调）
  Auth\ExternalAuth.vb           LittleSkin 外置登录 + authlib-injector
  SystemHelpers\JavaLocator.vb   本机 Java 探测（注册表+常见目录+PATH）
EasyerLauncher\               C# WinForms 主程序（Windows 11 风格 UI）
  Theme.cs                       主题引擎 + 卡片控件 + 悬停动画 + WIC webp 解码
  MainForm.cs                    侧边栏导航主窗体（页面滑入动画）
  Pages\*.cs                     主页 / 下载 / 加载器 / MOD / 账号 / 设置
tools\                        运维脚本（Azure 应用配置修补）
```

## 注意

- 游戏默认装在 `%APPDATA%\.easyer`，可在设置页改。
- Forge / NeoForge 安装会下载官方安装器并用本机 Java 执行 `--installClient`，第一次较慢属正常。
- NeoForge 支持 1.20.1+（1.20.1 走老的 `net/neoforged/forge` 坐标）。
- 想用自己的 Azure 应用？账号页的 Client ID 可改，留空或填错会自动回落到内置的。
