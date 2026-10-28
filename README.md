# Easyer Launcher 1

一个用 **C#（界面）+ VB.NET（核心库）** 写的 Minecraft Java 版启动器，Windows 11 / PCL2 风格。
**下载即用，不用装 .NET、不用填任何配置。**

> **此项目为 vibe code** —— 全程与 AI 结对、靠对话和截图迭代出来的，没有手写工程规范，也没有代码洁癖。
> 它能跑、能装游戏、能下 MOD，但请以「够用就好」的心态阅读源码。

## 下载

到 [Releases](https://github.com/Minecraftboyhei/easyer-Launcher-1/releases) 下 `EasyerLauncher.exe`，
**双击就能用**——单文件、自带 .NET 运行时，不用解压、不用安装、不用填任何配置（Win10/11 x64）。
程序只有一个 exe，删掉即卸载。**绿色便携**：配置与游戏默认放在 exe 同级的 `easyer-config.json` 和 `.minecraft`，
整个文件夹拷到 U 盘就能带走（设置页可改目录）。启动器自带更新检查，有新版本一键升级。

## 登录方式

| 方式 | 说明 | 现在能用吗 |
|---|---|---|
| 离线登录 | 输入昵称直接玩，UUID 用 authlib 官方离线算法 | ✅ |
| LittleSkin 外置登录 | 免微软账号，注册 [littleskin.cn](https://littleskin.cn) 建角色即可；自动下载并挂载 authlib-injector | ✅ |
| Microsoft 正版登录 | 登录应用已内置，账号页点「浏览器登录」即可，**无需填 Client ID** | ✅ |

> **关于正版登录**：Mojang 从 2023 年起对第三方启动器实行 **Client ID 白名单**，自建应用必须通过
> [Java Edition API 审核](https://aka.ms/mce-reviewappid)才能调用 `login_with_xbox`。
> 本项目的应用**已通过审核**，正版登录完整可用；令牌过期会自动刷新。
> 想用自建 Azure 应用？账号页点「高级 · 使用自建的应用」展开填写，留空即用内置的。

## 功能

| 功能 | 说明 |
|---|---|
| 版本下载 | 全部 Mojang 官方版本（正式版/快照/Beta/Alpha），带 SHA1 校验；安装时弹窗直接选加载器 |
| 加载器 | Fabric / Quilt（meta API 直装）、Forge / NeoForge（官方安装器 + BMCLAPI 镜像双源，需本机 Java） |
| MOD / 资源 | Modrinth 源搜索下载：MOD、资源包、光影、数据包，图标卡片网格，自动放入对应目录 |
| 多主题 | Win11 深色 / 浅色 / 完全自定义（背景+卡片+文字色）+ 6 种强调色预设 |
| 国内加速 | 版本/库/资源/加载器全部走 BMCLAPI 镜像优先、官方源兜底 |
| 体验 | 页面切换动画、悬停渐变、图形化版本/内容卡片（带版本与加载器图标）、账号头像 |
| 其他 | Java 自动检测、内存/分辨率设置、直连服务器参数 |
| 自动更新 | 启动时后台检查 GitHub Releases（含镜像兜底），一键下载并自动替换重启，可在设置页关闭 |
| 软件声明 | 首次启动展示声明：AI 参与开发、非 Mojang 官方、第三方服务致谢、隐私与免责（侧边栏「关于与声明」可随时查看） |

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
  Icons.cs                       矢量图标库（自绘，不依赖系统字体字形）+ 加载器徽标
  Avatars.cs                     账号头像（Crafatar + 本地 Steve 兜底）+ 头像控件
  ProgressRing.cs                旋转进度环（后台任务指示）
  Updater.cs                     GitHub Releases 检查 / 下载 / 自替换重启 + 更新对话框
  AboutDialog.cs                 软件声明与关于
  AppInfo.cs                     版本号与仓库元信息
  MainForm.cs                    侧边栏导航主窗体（页面滑入动画）
  Pages\*.cs                     主页 / 下载 / 加载器 / MOD / 整合包 / 账号 / 设置
tools\                        运维脚本（Azure 应用配置修补）
```

## 注意

- 游戏默认装在 exe 同级的 `.minecraft`（便携），可在设置页改。
- Forge / NeoForge 安装会下载官方安装器并用本机 Java 执行 `--installClient`，第一次较慢属正常。
- NeoForge 支持 1.20.1+（1.20.1 走老的 `net/neoforged/forge` 坐标）。
- 想用自己的 Azure 应用？账号页的 Client ID 可改，留空或填错会自动回落到内置的。
