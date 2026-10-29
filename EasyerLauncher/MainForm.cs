using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using EasyerLauncher.Core.Config;

namespace EasyerLauncher
{
    /// <summary>主窗体：Win11 风格侧边栏导航。</summary>
    public class MainForm : Form
    {
        public LauncherConfig Cfg;
        public SidebarPanel Sidebar;
        public Panel Content;
        public Label StatusLabel;
        public W11Button[] NavButtons;
        public Dictionary<string, Control> Pages = new();
        public string CurrentPage = "";

        private ProgressRing _ring;
        private Label _updateHint;
        private Label _verLabel;
        private Updater.ReleaseInfo _pendingUpdate;

        private static readonly (string Key, string Title, string Icon)[] Nav =
        {
            ("home",     "主页",       "home"),
            ("download", "下载",       "download"),
            ("loaders",  "加载器",     "wrench"),
            ("mods",     "模组 / 资源", "puzzle"),
            ("packs",    "整合包",      "package"),
            ("account",  "账号",       "user"),
            ("settings", "设置",       "settings"),
        };

        public MainForm()
        {
            Cfg = LauncherConfig.Load();
            // 便携模式：默认用 exe 同级的 .minecraft（老数据自动迁移/联接）
            Cfg.GameDirectory = LauncherConfig.EnsureGameDirectory(Cfg);
            try { Cfg.Save(); } catch { }

            Text = AppInfo.AppName + "  " + AppInfo.Display;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1240, 780);
            MinimumSize = new Size(1024, 660);
            Font = Theme.BodyFont;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;

            BuildUi();
            Theme.Changed += () =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(() =>
                {
                    Theme.EnableWin11Window(this);
                    Theme.ApplyTo(this);
                    Sidebar.BackColor = Theme.Sidebar;
                    Sidebar.Invalidate();
                    Content.BackColor = Theme.Background;
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
                else
                    StartBackgroundChecks();
            };
            Navigate("home");
            FormClosing += (s, e) => Anim.Shutdown();
            // 滚轮平滑滚动（弹窗不受影响）
            var smooth = new SmoothScrollFilter(this);
            Application.AddMessageFilter(smooth);
            FormClosed += (s, e) => Application.RemoveMessageFilter(smooth);
        }

        private void StartBackgroundChecks()
        {
            // 首次运行弹一次软件声明
            if (Cfg.AgreementVersion < 1)
            {
                try
                {
                    using var dlg = new AboutDialog(true);
                    dlg.ShowDialog(this);
                    Cfg.AgreementVersion = 1;
                    Cfg.Save();
                }
                catch { }
            }

            // 后台静默检查更新
            if (!Cfg.AutoCheckUpdate) return;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var rel = await Updater.CheckAsync();
                    if (rel == null) return;
                    _pendingUpdate = rel;
                    BeginInvoke(new Action(() =>
                    {
                        _updateHint.Text = "发现新版本 " + rel.Version + " · 点此更新";
                        _updateHint.Visible = true;
                        SetStatus("有新版本可用：" + rel.Version + "（当前 " + AppInfo.Display + "）");
                    }));
                }
                catch { }
            });
        }

        /// <summary>诊断：把当前页面的控件树（类型 / 位置 / 可见性）写成文本，用于排查"页面空白"。</summary>
        public string DumpPageTree()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Form=" + Size + " Content.ClientSize=" + Content.ClientSize);
            if (_pageHost != null)
                sb.AppendLine("pageHost=" + _pageHost.Bounds + " visible=" + _pageHost.Visible);
            void Walk(Control c, int depth)
            {
                var pad = new string(' ', depth * 2);
                sb.AppendLine(pad + c.GetType().Name + " '" + (c.Text ?? "").Replace("\r", "").Replace("\n", "") +
                    "' b=" + c.Bounds + " vis=" + c.Visible + " dock=" + c.Dock);
                foreach (Control child in c.Controls) Walk(child, depth + 1);
            }
            if (_pageHost != null) Walk(_pageHost, 0);
            return sb.ToString();
        }

        /// <summary>自拍模式：遍历主要页面各截一张，然后退出。</summary>
        private async System.Threading.Tasks.Task SelfShots(string dir)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                foreach (var key in new[] { "account", "home", "download", "mods", "packs", "loaders", "settings" })
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
            Sidebar = new SidebarPanel { Dock = DockStyle.Left, Width = 228, BackColor = Theme.Sidebar };
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
                    Padding = new Padding(14, 0, 0, 0),
                    // 透明才能露出侧边栏自己画的滑动药丸；不透明的话药丸永远被盖住
                    BackColor = Color.Transparent
                };
                string key = item.Key;
                btn.Click += (s, e) => Navigate(key);
                NavButtons[i] = btn;
                Sidebar.Controls.Add(btn);
                y += 48;
            }

            // 关于 / 声明
            var aboutBtn = new W11Button
            {
                Text = "关于与声明",
                IconGlyph = "info",
                ButtonKind = W11Button.Kind.Ghost,
                Font = Theme.SmallFont,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Bounds = new Rectangle(14, 636, 200, 36),
                MyTextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            aboutBtn.Click += (s, e) => { using var d = new AboutDialog(false); d.ShowDialog(this); };
            Sidebar.Controls.Add(aboutBtn);

            // 版本号（程序集版本，1.X.X）
            _verLabel = new Label
            {
                Text = AppInfo.Display + " · C# / VB.NET",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(24, 684),
                AutoSize = true
            };
            Sidebar.Controls.Add(_verLabel);

            // ===== 内容区（页面自己带滚动，外层不再 AutoScroll，省掉一次布局抖动）=====
            // 底色必须和页面一致：转场时新旧页会错位一两帧，露出来的就是这层
            Content = new Panel { Dock = DockStyle.Fill, AutoScroll = false, BackColor = Theme.Background };
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
            _ring = new ProgressRing { Dock = DockStyle.Right, Width = 220, Visible = false };
            _updateHint = new Label
            {
                Dock = DockStyle.Right,
                Width = 0,
                TextAlign = ContentAlignment.MiddleRight,
                Font = Theme.SmallFont,
                ForeColor = Color.FromArgb(52, 211, 153),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _updateHint.Click += (s, e) =>
            {
                if (_pendingUpdate == null) return;
                using var d = new UpdateDialog(_pendingUpdate);
                d.ShowDialog(this);
            };
            statusPanel.Controls.Add(StatusLabel);
            statusPanel.Controls.Add(_ring);
            statusPanel.Controls.Add(_updateHint);
            statusPanel.Controls.SetChildIndex(_updateHint, 0);
            statusPanel.Controls.SetChildIndex(_ring, 1);
            statusPanel.Controls.SetChildIndex(StatusLabel, 2);
            Controls.Add(statusPanel);

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

        // ================= 后台任务指示 =================

        /// <summary>任务状态变化：caption / 进度(-1=不确定) / 是否进行中。</summary>
        public event Action<string, float, bool> TaskState;

        public void BeginTask(string caption)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => BeginTask(caption))); return; }
            _ring.Start(caption, -1f);
            TaskState?.Invoke(caption, -1f, true);
        }

        public void UpdateTask(float pct, string caption = null)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => UpdateTask(pct, caption))); return; }
            _ring.Set(pct, caption);
            TaskState?.Invoke(caption, pct, true);
        }

        public void EndTask()
        {
            if (InvokeRequired) { BeginInvoke(new Action(EndTask)); return; }
            _ring.Stop();
            TaskState?.Invoke("", 0f, false);
        }

        /// <summary>从进度消息里抠出 (3/120) 之类的数字，转成百分比喂给进度环。</summary>
        public void ReportProgress(string msg)
        {
            SetStatus(msg);
            var m = Regex.Match(msg ?? "", @"(\d+)\s*/\s*(\d+)");
            if (m.Success && int.TryParse(m.Groups[2].Value, out var total) && total > 0 &&
                int.TryParse(m.Groups[1].Value, out var cur))
            {
                UpdateTask(Math.Min(1f, cur / (float)total), msg);
            }
            else
            {
                var pm = Regex.Match(msg ?? "", @"(\d+(?:\.\d+)?)\s*%");
                if (pm.Success && float.TryParse(pm.Groups[1].Value, out var p))
                    UpdateTask(Math.Min(1f, p / 100f), msg);
            }
        }

        /// <summary>加载器装好后刷新主页版本列表（丢弃缓存的主页，下次导航重建）。</summary>
        public void RefreshHomeVersions()
        {
            Pages.Remove("home");
            if (CurrentPage == "home") Navigate("home", true);
        }

        public void Navigate(string key) => Navigate(key, false);

        public void Navigate(string key, bool force)
        {
            // 已经在当前页，不重复播动画（否则点同一项会"抖"一下）
            if (!force && CurrentPage == key && _pageHost != null && !_pageHost.IsDisposed) return;

            CurrentPage = key;

            // ---- 1) 先给反馈：导航立即点亮，药丸开始滑；页面再慢慢来 ----
            int idx = -1;
            for (int i = 0; i < NavButtons.Length; i++)
            {
                bool sel = Nav[i].Key == key;
                if (sel) idx = i;
                if (NavButtons[i].Selected != sel)
                {
                    NavButtons[i].Selected = sel;
                    NavButtons[i].Invalidate();
                }
            }
            MovePillTo(idx, _pillReady);
            _pillReady = true;

            // ---- 2) 清掉上一轮的残局（快速连点时会有多份 host 叠着）----
            _navAnim?.Stop();
            _navAnim = null;
            var oldHost = _pageHost;
            DropStaleHosts(oldHost);

            if (!Pages.ContainsKey(key))
            {
                Control page = key switch
                {
                    "home" => new Pages.HomePage(this),
                    "download" => new Pages.DownloadsPage(this),
                    "loaders" => new Pages.LoadersPage(this),
                    "mods" => new Pages.ModsPage(this),
                    "packs" => new Pages.ModpacksPage(this),
                    "account" => new Pages.AccountPage(this),
                    "settings" => new Pages.SettingsPage(this),
                    _ => new Panel()
                };
                Pages[key] = page;
            }

            var p = Pages[key];
            p.Dock = DockStyle.Fill;
            if (p.Parent != null && !ReferenceEquals(p.Parent, oldHost))
                p.Parent.Controls.Remove(p);

            // 固定尺寸的 host：动画只改 Top，不改 Size —— 子页面因此不会被反复重排（卡顿根因）
            var host = new Panel
            {
                Bounds = new Rectangle(0, kPageSlide, Content.ClientSize.Width, Content.ClientSize.Height),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Theme.Background
            };
            host.SuspendLayout();
            host.Controls.Add(p);
            Content.Controls.Add(host);
            host.BringToFront();          // Controls[0] 才是最上层，Add 默认加到后面会被旧页盖住
            Theme.ApplyTo(p);
            // 必须用 true：ResumeLayout(false) 会让页面的 Dock.Fill 永远不生效，页面卡在 200x100
            host.ResumeLayout(true);
            _pageHost = host;

            // ---- 3) 转场：新页从下方轻推上来，旧页同时被顶走一点点 ----
            AnimatePageIn(host, oldHost);
        }

        private const int kPageSlide = 30;

        private Anim.Handle _navAnim;
        private Panel _pageHost;
        private Anim.Handle _pillAnim;
        private bool _pillReady;

        private void MovePillTo(int index, bool animate)
        {
            if (index < 0 || index >= NavButtons.Length || Sidebar.IsDisposed) return;
            var b = NavButtons[index];
            float toTop = b.Top;
            float toH = b.Height;

            if (!animate || !Sidebar.PillVisible)
            {
                Sidebar.PillVisible = true;
                Sidebar.PillTop = toTop;
                Sidebar.PillHeight = toH;
                Sidebar.Invalidate();
                return;
            }

            _pillAnim?.Stop();
            float fromTop = Sidebar.PillTop, fromH = Sidebar.PillHeight;
            if (Math.Abs(fromTop - toTop) < 0.5f) return;

            _pillAnim = Anim.Run(300, Ease.OutQuart, t =>
            {
                if (Sidebar.IsDisposed) return;
                Sidebar.MovePill(fromTop + (toTop - fromTop) * t, fromH + (toH - fromH) * t);
            });
        }

        /// <summary>清掉除 keep 以外的 host（快速连点会留下正在退场的旧 host）。</summary>
        private void DropStaleHosts(Control keep)
        {
            var stale = new List<Control>();
            foreach (Control c in Content.Controls)
                if (!ReferenceEquals(c, keep)) stale.Add(c);
            foreach (var c in stale)
            {
                try { c.Controls.Clear(); } catch { }   // 先摘掉子页面，别跟着被 Dispose
                try { Content.Controls.Remove(c); c.Dispose(); } catch { }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_pageHost != null && !_pageHost.IsDisposed)
                _pageHost.Bounds = new Rectangle(0, _navAnim != null ? _pageHost.Top : 0,
                    Content.ClientSize.Width, Content.ClientSize.Height);
        }

        /// <summary>
        /// 页面转场：新页从下方滑入（30px，280ms，easeOutQuint），旧页同时上移离场并被覆盖。
        /// 旧页留到动画结束才移除 —— 否则中途露出容器底色，切换会"闪一下"。
        /// </summary>
        private void AnimatePageIn(Panel host, Panel oldHost)
        {
            const double durMs = 280;
            const int slide = kPageSlide;

            _navAnim = Anim.Run(durMs, Ease.OutQuint, t =>
            {
                if (host.IsDisposed) return;
                host.Top = (int)Math.Round(slide * (1f - t));
                if (oldHost != null && !oldHost.IsDisposed)
                    oldHost.Top = (int)Math.Round(-slide * 0.42f * t);
            }, () =>
            {
                if (!host.IsDisposed) host.Top = 0;
                if (oldHost != null && !oldHost.IsDisposed)
                {
                    try { oldHost.Controls.Clear(); } catch { }
                    try { Content.Controls.Remove(oldHost); oldHost.Dispose(); } catch { }
                }
                _navAnim = null;
            }, host);
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

            BeginTask("正在准备启动 " + versionId + " ...");
            SetStatus("正在准备启动 " + versionId + " ...");
            try
            {
                // 正版账号：令牌 24 小时过期，启动前自动续期
                if (acc.AccountType == "microsoft" && !string.IsNullOrEmpty(acc.MsRefreshToken))
                {
                    bool needRefresh;
                    if (DateTime.TryParse(acc.MsExpiresAt, out var exp))
                        needRefresh = DateTime.UtcNow > exp.AddHours(-2);
                    else
                        needRefresh = true;

                    if (needRefresh)
                    {
                        if (string.IsNullOrWhiteSpace(Cfg.MsClientId))
                        {
                            MessageBox.Show("正版登录已过期，但没保存 Azure Client ID，无法自动续期。\n" +
                                            "请到「账号」页重新登录一次。", "Easyer",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            SetStatus("正版令牌续期中 ...");
                            try
                            {
                                var prof = await System.Threading.Tasks.Task.Run(
                                    () => Core.Auth.MicrosoftAuth.Refresh(Cfg.MsClientId, acc.MsRefreshToken));
                                acc.Name = prof.Name;
                                acc.Uuid = prof.Uuid;
                                acc.AccessToken = prof.AccessToken;
                                acc.MsRefreshToken = prof.RefreshToken;
                                acc.MsExpiresAt = DateTime.UtcNow.AddHours(23).ToString("o");
                                Cfg.ActiveAccountName = acc.Name;
                                Cfg.Save();
                                SetStatus("正版令牌已续期 ✓");
                            }
                            catch (Exception rex)
                            {
                                MessageBox.Show("正版登录续期失败，请到「账号」页重新登录：\n" + rex.Message, "Easyer",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Navigate("account");
                                return;
                            }
                        }
                    }
                }

                // 外置登录账号：确保 authlib-injector 就位
                string injectorUrl = null, injectorJar = null;
                if (acc.AccountType == "littleskin")
                {
                    UpdateTask(-1f, "准备外置登录组件 ...");
                    injectorUrl = Core.Auth.ExternalAuth.AuthRoot;
                    injectorJar = await System.Threading.Tasks.Task.Run(
                        () => Core.Auth.ExternalAuth.EnsureInjectorJar(Cfg.GameDirectory));
                }

                UpdateTask(-1f, "正在启动 " + versionId + " ...");
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // 整合包实例：版本库共用全局目录，gameDir 指向实例目录（mods/存档隔离）
                    var inst = Cfg.Instances.Find(i =>
                        string.Equals(i.Id, versionId, StringComparison.OrdinalIgnoreCase));
                    var opt = new Core.Minecraft.GameLauncher.LaunchOptions
                    {
                        VersionId = versionId,
                        GameDir = inst != null && System.IO.Directory.Exists(inst.Dir) ? inst.Dir : Cfg.GameDirectory,
                        RootDir = Cfg.GameDirectory,
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
                        FullScreen = Cfg.FullScreen,
                        AuthlibInjectorUrl = injectorUrl,
                        AuthlibInjectorJar = injectorJar
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
            finally
            {
                EndTask();
            }
        }
    }
}
