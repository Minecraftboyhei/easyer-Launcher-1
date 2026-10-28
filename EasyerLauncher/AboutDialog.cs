using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>软件声明 / 关于：AI 创作说明、非官方声明、第三方致谢、隐私与免责。</summary>
    public class AboutDialog : Form
    {
        private const string Statement =
            "一、这是什么\r\n" +
            "Easyer Launcher 1 是一个 Windows 11 风格的 Minecraft Java 版启动器，采用 C# + VB.NET（.NET 8）编写，" +
            "由 AI 在人类指导下辅助开发。完全免费、开源、无广告、不收集任何个人信息。\r\n\r\n" +
            "二、关于 Minecraft\r\n" +
            "Minecraft 是 Mojang Studios 的商标。本软件不是 Mojang / Microsoft 官方产品，与官方没有任何隶属或合作关系。" +
            "使用正版登录功能需要你自己拥有 Minecraft 账号。\r\n\r\n" +
            "三、关于 AI 参与\r\n" +
            "本项目的大部分代码由 AI 生成并迭代，已经过人工测试，但不保证没有 bug。" +
            "出问题欢迎到 GitHub 提 Issue，能修就修，修不了会在 Issue 里说明。\r\n\r\n" +
            "四、第三方服务致谢\r\n" +
            "· BMCLAPI（bangbang93）：国内下载镜像，版本 / 库 / 资源走它才快\r\n" +
            "· MCIM：Modrinth API 镜像，模组与整合包搜索走它才通\r\n" +
            "· Modrinth：模组、整合包、资源包的数据来源\r\n" +
            "· LittleSkin：外置登录（Yggdrasil）服务\r\n" +
            "· authlib-injector：外置登录支持\r\n" +
            "· Crafatar：账号头像\r\n" +
            "以上服务由第三方提供，它们的可用性与内容不由本项目负责。\r\n\r\n" +
            "五、隐私说明\r\n" +
            "账号令牌、微软刷新令牌一律保存在本地配置文件 easyer-config.json（启动器所在目录），" +
            "不会上传到登录服务以外的任何地方。离线账号完全不联网。\r\n\r\n" +
            "六、免责声明\r\n" +
            "本软件按「原样」提供，不对存档损坏、账号异常或任何其它损失负责。请自行备份存档。\r\n\r\n" +
            "七、许可证\r\n" +
            "MIT License，详见仓库中的 LICENSE 文件。";

        public AboutDialog(bool firstRun)
        {
            Text = firstRun ? "使用前请阅读 — 软件声明" : "关于 Easyer Launcher 1";
            Size = new Size(620, 560);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var badge = new KindBadge { Kind = "vanilla", Location = new Point(24, 22), Size = new Size(52, 52) };
            Controls.Add(badge);

            Controls.Add(new Label
            {
                Text = AppInfo.AppName,
                Font = new Font(Theme.DefaultFontFamily, 15f, FontStyle.Bold),
                Location = new Point(90, 26),
                AutoSize = true
            });
            Controls.Add(new Label
            {
                Text = AppInfo.Display + "  ·  C# / VB.NET  ·  .NET 8  ·  Windows",
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                Location = new Point(92, 54),
                AutoSize = true
            });

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.BodyFont,
                Bounds = new Rectangle(24, 92, 556, 348),
                Text = Statement
            };
            Controls.Add(box);

            var repo = new W11Button
            {
                Text = "开源仓库",
                IconGlyph = "link",
                ButtonKind = W11Button.Kind.Standard,
                Bounds = new Rectangle(24, 456, 150, 42)
            };
            repo.Click += (s, e) => Open(AppInfo.RepoUrl);
            Controls.Add(repo);

            var issue = new W11Button
            {
                Text = "反馈问题",
                IconGlyph = "alert",
                ButtonKind = W11Button.Kind.Standard,
                Bounds = new Rectangle(186, 456, 150, 42)
            };
            issue.Click += (s, e) => Open(AppInfo.IssuesUrl);
            Controls.Add(issue);

            var close = new W11Button
            {
                Text = firstRun ? "我已阅读，继续" : "关闭",
                IconGlyph = "check",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(430, 456, 150, 42)
            };
            close.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(close);
            AcceptButton = null;
            CancelButton = null;

            Theme.ApplyTo(this);
            Theme.EnableWin11Window(this);
        }

        private static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
    }
}
