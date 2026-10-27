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
**本应用的 Client ID 已通过 Mojang API 白名单审核，正版登录完整可用**，微软账号登录即玩。
想用自建 Azure 应用？账号页点「高级 · 使用自建的应用」展开填写；留空即用内置。

### 已知说明
- Forge / NeoForge 首次安装需下载官方安装器并用本机 Java 执行，较慢属正常
- NeoForge 支持 1.20.1+（1.20.1 走老的 `net/neoforged/forge` 坐标）
- 仅支持 Windows x64

## v1.0.1 更新（2026-10-27）

- 正版登录全面可用：应用已通过 Mojang API 审核
- 账号页默认隐藏 Client ID，自建入口收进「高级」按钮
