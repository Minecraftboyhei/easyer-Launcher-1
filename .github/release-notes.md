## Easyer Launcher 1 — 首个公开版本

Windows 11 / PCL2 风格的 Minecraft Java 版启动器，**下载解压双击即用，无需安装 .NET、无需填任何配置**。

### 这个包是什么
`EasyerLauncher.exe` —— 单文件绿色版，自带 .NET 8 运行时，Win10/11 x64 直接双击运行。
配置与游戏目录默认在 `%APPDATA%\.easyer`（设置页可改）。

### 能干什么
- **版本下载**：全部 Mojang 官方版本（正式版/快照/Beta/Alpha），SHA1 校验，安装时弹窗直接选加载器
- **加载器**：Fabric / Quilt / Forge / NeoForge，官方安装器 + BMCLAPI 镜像双源
- **MOD / 资源**：Modrinth 搜索下载 MOD、资源包、光影、数据包，图标卡片网格
- **登录**：离线登录 ✅ / LittleSkin 外置登录 ✅ / Microsoft 正版登录（登录应用已内置，见下）
- **国内加速**：版本、库、资源、加载器全部 BMCLAPI 镜像优先
- **多主题**：Win11 深色 / 浅色 / 自定义 + 6 种强调色，页面切换与悬停动画

### 关于微软正版登录
登录用的 Azure 应用已经编进程序里，账号页点「浏览器登录」即可，不用自己注册应用。
但 Mojang 自 2023 年起对第三方启动器实行 **Client ID 白名单**：自建应用必须先通过
[Java Edition API 审核](https://aka.ms/mce-reviewappid)。本项目的应用已提交审核，
**审核通过前，正版登录会在最后一步被 Mojang 拦下**（微软账号与 Xbox 认证本身都能通过）。
审核结果出来后会在这里说明；期间请用离线 / LittleSkin 登录，游戏体验完全一致。

### 已知说明
- Forge / NeoForge 首次安装需下载官方安装器并用本机 Java 执行，较慢属正常
- NeoForge 支持 1.20.1+（1.20.1 走老的 `net/neoforged/forge` 坐标）
- 仅支持 Windows x64
