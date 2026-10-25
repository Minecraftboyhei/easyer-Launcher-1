using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EasyerLauncher.Core.Config;

namespace EasyerLauncher
{
    /// <summary>主窗体：Win11 风格侧边栏导航。</summary>
    public class MainForm : Form
    {
        public LauncherConfig Cfg;
        public Panel Sidebar;
        public Panel Content;
        public Label StatusLabel;
        public W11Button[] NavButtons;
        public Dictionary<string, Control> Pages = new();
        public string CurrentPage = "";

        private static readonly (string Key, string Title, string Icon)[] Nav =
        {
            ("home",     "主页",       "\uE80F"),  // Home
            ("download", "下载",       "\uE896"),  // Download
            ("loaders",  "加载器",     "\uE90F"),  // Repair
            ("mods",     "模组 / 资源", "\uEA86"),  // Puzzle
            ("account",  "账号",       "\uE77B"),  // Contact
            ("settings", "设置",       "\uE713"),  // Setting s
        };

        public MainForm()
        {
            Cfg = LauncherConfig.Load();
            if (string.IsNullOrEmpty(Cfg.GameDirectory)) Cfg.GameDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.easyer";

            Text = "Easyer Launcher 1";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1240, 780);
            MinimumSize = new Size(1024, 660);
            Font = Theme.BodyFont;

            BuildUi();
            Theme.Changed += () =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(() =>
                {
                    Theme.EnableWin11Window(this);
                    Theme.ApplyTo(this);
                    Sidebar.BackColor = Theme.Sidebar;
                    foreach (var b in NavButtons) b.Invalidate();
                    Invalidate();
                }));
            };
            Theme.ApplyTo(this);
            Sidebar.BackColor = Theme.Sidebar;
            Shown += (s, e) =>
            {
                Theme.EnableWin11Window(this);
                var args = Environment.GetCommandLineArgs();
                int shotIdx = Array.IndexOf(args, "--shots");
                if (shotIdx >= 0 && shotIdx + 1 < args.Length)
                    _ = SelfShots(args[shotIdx + 1]);
            };
            Navigate("home");
        }

        /// <summary>自拍模式：遍历主要页面各截一张，然后退出。</summary>
        private async System.Threading.Tasks.Task SelfShots(string dir)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                foreach (var key in new[] { "home", "download", "mods", "loaders" })
                {
                    Navigate(key);
                    await System.Threading.Tasks.Task.Delay(key == "mods" || key == "download" ? 4500 : 2000);
                    using var bmp = new Bitmap(Width, Height);
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(System.IO.Path.Combine(dir, key + ".png"));
                }
                Application.Exit();
            }
            catch { Application.Exit(); }
        }

        private void BuildUi()
        {
            // ===== 侧边栏 =====
            Sidebar = new Panel { Dock = DockStyle.Left, Width = 228, BackColor = Theme.Sidebar };
            Controls.Add(Sidebar);

            var logo = new Label
            {
                Text = "Easyer Launcher",
                Font = new Font(Theme.DefaultFontFamily, 15f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                BackColor = Color.Transparent,
                Location = new Point(22, 22),
                AutoSize = true
            };
            var logoSub = new Label
            {
                Text = "简单点，启动个游戏而已",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(24, 54),
                AutoSize = true
            };
            Sidebar.Controls.Add(logo);
            Sidebar.Controls.Add(logoSub);

            NavButtons = new W11Button[Nav.Length];
            int y = 96;
            for (int i = 0; i < Nav.Length; i++)
            {
                var item = Nav[i];
                var btn = new W11Button
                {
                    Text = item.Title,
                    IconGlyph = item.Icon,
                    ButtonKind = W11Button.Kind.Nav,
                    Bounds = new Rectangle(14, y, 200, 44),
                    MyTextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(14, 0, 0, 0)
                };
                string key = item.Key;
                btn.Click += (s, e) => Navigate(key);
                NavButtons[i] = btn;
                Sidebar.Controls.Add(btn);
                y += 48;
            }

            // 版本号
            var ver = new Label
            {
                Text = "v1.0 · C#/VB.NET",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(24, 700),
                AutoSize = true
            };
            Sidebar.Controls.Add(ver);

            // ===== 内容区 =====
            Content = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            Controls.Add(Content);

            // ===== 状态栏 =====
            var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 30 };
            StatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                Padding = new Padding(10, 0, 0, 0)
            };
            statusPanel.Controls.Add(StatusLabel);
            Controls.Add(statusPanel);

            // Dock 顺序：Bottom 先加会被 Fill 覆盖，调整一下
            Controls.SetChildIndex(Content, 0);
            Controls.SetChildIndex(Sidebar, 1);
            Controls.SetChildIndex(statusPanel, 0);
        }

        public void SetStatus(string msg)
        {
            if (StatusLabel.InvokeRequired)
                StatusLabel.BeginInvoke(new Action(() => StatusLabel.Text = msg));
            else
                StatusLabel.Text = msg;
        }

        /// <summary>加载器装好后刷新主页版本列表（丢弃缓存的主页，下次导航重建）。</summary>
        public void RefreshHomeVersions()
        {
            Pages.Remove("home");
            if (CurrentPage == "home") Navigate("home");
        }

        public void Navigate(string key)
        {
            CurrentPage = key;
            Content.Controls.Clear();
            if (_pageHost != null)
            {
                try { _pageHost.Controls.Clear(); _pageHost.Dispose(); } catch { }
                _pageHost = null;
            }
            _navAnim?.Stop();

            if (!Pages.ContainsKey(key))
            {
                Control page = key switch
                {
                    "home" => new Pages.HomePage(this),
                    "download" => new Pages.DownloadsPage(this),
                    "loaders" => new Pages.LoadersPage(this),
                    "mods" => new Pages.ModsPage(this),
                    "account" => new Pages.AccountPage(this),
                    "settings" => new Pages.SettingsPage(this),
                    _ => new Panel()
                };
                Pages[key] = page;
            }

            var p = Pages[key];
            p.Dock = DockStyle.Fill;
            _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(0, 34, 0, 0) };
            _pageHost.Controls.Add(p);
            Content.Controls.Add(_pageHost);
            Theme.ApplyTo(p);
            p.Invalidate();
            AnimatePageIn(_pageHost);

            for (int i = 0; i < NavButtons.Length; i++)
                NavButtons[i].Selected = Nav[i].Key == key;
            foreach (var b in NavButtons) b.Invalidate();
        }

        private System.Windows.Forms.Timer _navAnim;
        private Panel _pageHost;

        /// <summary>页面滑入动画（ease-out，约 170ms）。</summary>
        private void AnimatePageIn(Panel host)
        {
            const int startY = 34;
            const int frames = 12;
            int frame = 0;
            _navAnim?.Dispose();
            _navAnim = new System.Windows.Forms.Timer { Interval = 14 };
            _navAnim.Tick += (s, e) =>
            {
                if (host.IsDisposed) { _navAnim.Stop(); _navAnim.Dispose(); return; }
                frame++;
                float p = Math.Min(1f, frame / (float)frames);
                float ease = 1f - (1f - p) * (1f - p);   // ease-out quad
                host.Padding = new Padding(0, (int)Math.Round(startY * (1f - ease)), 0, 0);
                if (p >= 1f) { _navAnim.Stop(); _navAnim.Dispose(); }
            };
            _navAnim.Start();
        }

        /// <summary>启动游戏（主页按钮触发）。</summary>
        public async void LaunchGame(string versionId)
        {
            var acc = Cfg.GetActiveAccount();
            if (acc == null)
            {
                MessageBox.Show("还没有账号！去「账号」页添加一个（离线账号秒建）。", "Easyer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Navigate("account");
                return;
            }

            SetStatus("正在准备启动 " + versionId + " ...");
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    var opt = new Core.Minecraft.GameLauncher.LaunchOptions
                    {
                        VersionId = versionId,
                        GameDir = Cfg.GameDirectory,
                        JavaPath = Cfg.JavaPath,
                        MaxMemoryMB = Cfg.MaxMemoryMB,
                        MinMemoryMB = 512,
                        Account = new Core.Config.AccountInfo
                        {
                            Name = acc.Name, Uuid = acc.Uuid,
                            AccessToken = acc.AccessToken, AccountType = acc.AccountType
                        },
                        WindowWidth = Cfg.WindowWidth,
                        WindowHeight = Cfg.WindowHeight,
                        FullScreen = Cfg.FullScreen
                    };
                    Core.Minecraft.GameLauncher.Launch(opt);
                });
                SetStatus("游戏已启动：" + versionId + " ✓  （玩家: " + acc.Name + "）");
            }
            catch (Exception ex)
            {
                SetStatus("启动失败：" + ex.Message);
                MessageBox.Show("启动失败：\n" + ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
