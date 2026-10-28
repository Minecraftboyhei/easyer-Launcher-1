using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;
using EasyerLauncher.Core.Marketplace;
using EasyerLauncher.Core.SystemHelpers;

namespace EasyerLauncher.Pages
{
    /// <summary>工具：往内容面板里塞一页的骨架。</summary>
    internal static class PageUtil
    {
        public static Label Title(string text)
        {
            return new Label
            {
                Text = text,
                Font = Theme.TitleFont,
                ForeColor = Theme.Foreground,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(32, 24)
            };
        }
    }

    /// <summary>账号列表（自绘：头像 + 昵称 + 类型 + 当前标记）。</summary>
    internal class AccountList : Control
    {
        public const int RowH = 58;

        private List<Core.Config.AccountInfo> _items = new();
        private string _activeName = "";
        private int _selected = -1;
        private readonly System.Windows.Forms.Timer _t;

        public event Action SelectionChanged;

        public Core.Config.AccountInfo Selected =>
            _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

        public int SelectedIndex => _selected;

        public AccountList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = RowH * 3;
            // 头像是异步拉的，落地后补刷一次
            _t = new System.Windows.Forms.Timer { Interval = 700 };
            _t.Tick += (s, e) => { _t.Stop(); if (!IsDisposed) Invalidate(); };
        }

        public void SetAccounts(List<Core.Config.AccountInfo> items, string activeName)
        {
            _items = items ?? new List<Core.Config.AccountInfo>();
            _activeName = activeName ?? "";
            if (_selected >= _items.Count) _selected = _items.Count - 1;
            if (_selected < 0 && _items.Count > 0) _selected = 0;
            Invalidate();
            _t.Stop();
            _t.Start();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int row = e.Y / RowH;
            if (row >= 0 && row < _items.Count)
            {
                _selected = row;
                Invalidate();
                SelectionChanged?.Invoke();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int row = e.Y / RowH;
            if (row >= 0 && row < _items.Count) { _selected = row; Invalidate(); OnActivate?.Invoke(row); }
        }

        public event Action<int> OnActivate;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            if (_items.Count == 0)
            {
                TextRenderer.DrawText(g, "还没有账号 — 右边加一个离线账号，10 秒就能开玩。",
                    Theme.BodyFont, new Rectangle(12, 12, Math.Max(60, Width - 24), 24),
                    Theme.ForegroundDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                var acc = _items[i];
                var rect = new Rectangle(0, i * RowH + 3, Width - 1, RowH - 6);
                bool sel = i == _selected;
                bool active = string.Equals(acc.Name, _activeName, StringComparison.OrdinalIgnoreCase);

                using (var path = Theme.RoundedPath(rect, 10))
                {
                    using (var b = new SolidBrush(sel ? Theme.Lerp(Theme.Card, Theme.Accent, 0.18f) : Theme.Card))
                        g.FillPath(b, path);
                    using (var pen = new Pen(active ? Theme.Accent : Theme.Border))
                        g.DrawPath(pen, path);
                }

                // 头像：方形不裁圆、最近邻缩放（PCL 风格像素脸）
                var av = new Rectangle(rect.X + 10, rect.Y + 5, 48, 48);
                var img = Avatars.GetCached(acc) ?? Avatars.Steve();
                var pm = g.InterpolationMode;
                var po = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, av);
                g.InterpolationMode = pm;
                g.PixelOffsetMode = po;

                // 昵称
                var nameRect = new Rectangle(av.Right + 12, rect.Y + 8, Math.Max(60, rect.Width - av.Right - 110), 22);
                TextRenderer.DrawText(g, acc.Name, new Font(Theme.DefaultFontFamily, 10.5f, FontStyle.Bold),
                    nameRect, sel ? Theme.Accent : Theme.Foreground,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // 类型 / 认证服
                string tag = acc.AccountType switch
                {
                    "microsoft" => "正版 · Microsoft",
                    "littleskin" => "外置 · " + (string.IsNullOrEmpty(acc.AuthServer) ? "LittleSkin" : acc.AuthServer),
                    _ => "离线"
                };
                TextRenderer.DrawText(g, tag, Theme.SmallFont,
                    new Rectangle(av.Right + 12, rect.Y + 30, Math.Max(60, rect.Width - av.Right - 110), 18),
                    Theme.ForegroundDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (active)
                {
                    var badge = new Rectangle(rect.Right - 74, rect.Y + 16, 62, 22);
                    using (var bp = Theme.RoundedPath(badge, 11))
                    using (var bb = new SolidBrush(Color.FromArgb(46, Theme.Accent)))
                        g.FillPath(bb, bp);
                    TextRenderer.DrawText(g, "当前", Theme.SmallFont, badge, Theme.Accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    // ================= 主页 =================
    public class AccountPage : Panel
    {
        private readonly MainForm _main;
        private AccountList _list;
        private TextBox _offlineName;
        private TextBox _clientId;
        private TextBox _extUser, _extPass;
        private bool _deviceCodeWaiting = false;
        private W11Card _leftCard, _rightCard;

        public AccountPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            Build();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutCards();
        }

        /// <summary>两卡自适应：按内容宽度对半分，不超出窗口。</summary>
        private void LayoutCards()
        {
            if (_leftCard == null || _rightCard == null) return;
            int total = Math.Max(900, Width - 64);
            int rightW = 480;                                   // 右卡固定宽度，保证说明文字不裁边
            int leftW = Math.Max(340, total - rightW - 20);     // 左卡吃剩余空间
            _leftCard.Bounds = new Rectangle(32, 90, leftW, 360);
            _rightCard.Bounds = new Rectangle(32 + leftW + 20, 90, rightW, 640);
            _list.Bounds = new Rectangle(16, 16, leftW - 32, 220);
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("账号"));

            // 账号列表
            _leftCard = new W11Card { Bounds = new Rectangle(32, 90, 560, 360) };
            Controls.Add(_leftCard);
            _list = new AccountList { Bounds = new Rectangle(16, 16, 528, 220) };
            _list.OnActivate += idx => UseSelected();
            _leftCard.Controls.Add(_list);

            var useBtn = new W11Button { Text = "切换为当前账号", IconGlyph = "check", Bounds = new Rectangle(16, 250, 190, 38) };
            useBtn.Click += (s, e) => UseSelected();
            _leftCard.Controls.Add(useBtn);

            var delBtn = new W11Button { Text = "删除账号", IconGlyph = "trash", Bounds = new Rectangle(216, 250, 130, 38) };
            delBtn.Click += (s, e) =>
            {
                var a = _list.Selected;
                if (a == null) return;
                if (MessageBox.Show("删除账号 " + a.Name + "？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _main.Cfg.Accounts.Remove(a);
                    if (_main.Cfg.ActiveAccountName == a.Name)
                        _main.Cfg.ActiveAccountName = _main.Cfg.Accounts.Count > 0 ? _main.Cfg.Accounts[0].Name : "";
                    _main.Cfg.Save();
                    RefreshList();
                }
            };
            _leftCard.Controls.Add(delBtn);

            // 离线登录
            _rightCard = new W11Card { Bounds = new Rectangle(612, 90, 480, 400) };
            Controls.Add(_rightCard);
            _rightCard.Controls.Add(new Label
            {
                Text = "离线账号（不需要正版）",
                Font = Theme.H2Font,
                BackColor = Color.Transparent,
                Location = new Point(20, 16),
                AutoSize = true
            });
            _rightCard.Controls.Add(new Label
            {
                Text = "输入一个游戏昵称就能玩（只能进离线/局域网服务器）：",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(20, 56),
                AutoSize = true
            });
            _offlineName = new TextBox
            {
                Bounds = new Rectangle(20, 88, 300, 32),
                BorderStyle = BorderStyle.FixedSingle
            };
            _rightCard.Controls.Add(_offlineName);

            var offlineBtn = new W11Button
            {
                Text = "添加离线账号",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(20, 134, 200, 40)
            };
            offlineBtn.Click += (s, e) =>
            {
                var name = _offlineName.Text.Trim();
                if (name.Length == 0)
                {
                    MessageBox.Show("昵称不能为空！", "Easyer");
                    return;
                }
                if (_main.Cfg.Accounts.Exists(a => a.Name == name))
                {
                    MessageBox.Show("这个账号已经存在了。", "Easyer");
                    return;
                }
                _main.Cfg.Accounts.Add(EasyerLauncher.Core.Auth.OfflineAuth.Login(name));
                if (string.IsNullOrEmpty(_main.Cfg.ActiveAccountName)) _main.Cfg.ActiveAccountName = name;
                _main.Cfg.Save();
                RefreshList();
                _main.SetStatus("离线账号 " + name + " 已添加 ✓");
            };
            _rightCard.Controls.Add(offlineBtn);

            // 正版登录
            _rightCard.Controls.Add(new Label
            {
                Text = "Microsoft 正版登录",
                Font = Theme.H2Font,
                BackColor = Color.Transparent,
                Location = new Point(20, 190),
                AutoSize = true
            });
            _rightCard.Controls.Add(new Label
            {
                Text = "已内置登录应用，开箱即用——无需填写任何 ID。\n" +
                       "本应用的 Client ID 已通过 Mojang API 白名单审核，\n" +
                       "点下方按钮用微软账号登录即可。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(20, 220),
                AutoSize = true
            });
            _clientId = new TextBox
            {
                Bounds = new Rectangle(20, 322, 300, 30),
                BorderStyle = BorderStyle.FixedSingle,
                // 内置 ID 不明文展示；只有用户自建了应用（配置值 ≠ 内置）才显示
                Text = _main.Cfg.MsClientId == EasyerLauncher.Core.Auth.MicrosoftAuth.DefaultClientId ? "" : _main.Cfg.MsClientId,
                PlaceholderText = "留空 = 使用内置登录应用",
                Visible = false
            };
            _rightCard.Controls.Add(_clientId);

            var azureBtn = new W11Button { Text = "去注册", Bounds = new Rectangle(330, 322, 94, 30), Visible = false };
            azureBtn.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                        "https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/CreateApplicationBlade")
                    { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show("打开浏览器失败：" + ex.Message, "Easyer"); }
            };
            _rightCard.Controls.Add(azureBtn);

            // 高级入口：默认收起，展开后才能看到/修改 Client ID
            var advancedBtn = new W11Button { Text = "高级 · 使用自建的应用 →", ButtonKind = W11Button.Kind.Ghost, Bounds = new Rectangle(20, 322, 200, 30) };
            advancedBtn.Click += (s, e) =>
            {
                advancedBtn.Visible = false;
                _clientId.Visible = true;
                azureBtn.Visible = true;
            };
            _rightCard.Controls.Add(advancedBtn);

            var msBtn = new W11Button { Text = "浏览器登录（推荐）", ButtonKind = W11Button.Kind.Accent, Bounds = new Rectangle(20, 364, 190, 40) };
            var dcBtn = new W11Button { Text = "设备码登录", ButtonKind = W11Button.Kind.Ghost, Bounds = new Rectangle(220, 366, 130, 38) };
            dcBtn.Click += (s, e) => _ = MicrosoftLogin();
            msBtn.Click += async (s, e) =>
            {
                // 收起状态或留空 = 用内置应用；展开且填了值才用自建
                var clientId = _clientId.Visible ? _clientId.Text.Trim() : "";
                if (clientId.Length == 0) clientId = _main.Cfg.MsClientId;
                if (!Guid.TryParse(clientId, out _))
                {
                    MessageBox.Show("Client ID 需要是有效的 GUID；留空则使用内置登录应用。", "Easyer");
                    return;
                }
                _main.Cfg.MsClientId = clientId;
                _main.Cfg.Save();

                if (_deviceCodeWaiting) return;
                _deviceCodeWaiting = true;
                try
                {
                    _main.SetStatus("已打开浏览器，等待微软授权 ...");
                    var prof = await System.Threading.Tasks.Task.Run(
                        () => Core.Auth.LoopbackLogin.Run(clientId, 300));

                    _main.Cfg.Accounts.RemoveAll(a => a.Name == prof.Name);
                    _main.Cfg.Accounts.Add(new Core.Config.AccountInfo
                    {
                        Name = prof.Name,
                        Uuid = prof.Uuid,
                        AccessToken = prof.AccessToken,
                        MsRefreshToken = prof.RefreshToken,
                        MsExpiresAt = DateTime.UtcNow.AddHours(23).ToString("o"),
                        AccountType = "microsoft"
                    });
                    _main.Cfg.ActiveAccountName = prof.Name;
                    _main.Cfg.Save();
                    RefreshList();
                    _main.SetStatus("正版账号 " + prof.Name + " 登录成功 ✓");
                }
                catch (Exception ex)
                {
                    _main.SetStatus("微软登录失败：" + ex.Message);
                    MessageBox.Show("登录失败：\n" + ex.Message, "Easyer",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { _deviceCodeWaiting = false; }
            };
            _rightCard.Controls.Add(dcBtn);
            _rightCard.Controls.Add(msBtn);

            // 外置登录（LittleSkin，免微软免 Azure）
            _rightCard.Controls.Add(new Label
            {
                Text = "外置登录 · LittleSkin（免微软免 Azure）",
                Font = Theme.H2Font,
                BackColor = Color.Transparent,
                Location = new Point(20, 420),
                AutoSize = true
            });
            _rightCard.Controls.Add(new Label
            {
                Text = "没有正版也能玩：去 littleskin.cn 免费注册个账号（只要邮箱），\n" +
                       "在这里填用户名和密码登录，皮肤用 LittleSkin 的。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(20, 452),
                AutoSize = true
            });
            _extUser = new TextBox
            {
                Bounds = new Rectangle(20, 508, 300, 30),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "用户名或邮箱"
            };
            _rightCard.Controls.Add(_extUser);
            _extPass = new TextBox
            {
                Bounds = new Rectangle(20, 544, 300, 30),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "密码",
                UseSystemPasswordChar = true
            };
            _rightCard.Controls.Add(_extPass);
            var extBtn = new W11Button
            {
                Text = "登录 LittleSkin",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(330, 544, 120, 30)
            };
            extBtn.Click += async (s, e) =>
            {
                var u = _extUser.Text.Trim();
                var p = _extPass.Text;
                if (u.Length == 0 || p.Length == 0)
                {
                    MessageBox.Show("用户名和密码都要填！", "Easyer");
                    return;
                }
                try
                {
                    var prof = await System.Threading.Tasks.Task.Run(
                        () => Core.Auth.ExternalAuth.Login(u, p));
                    _main.Cfg.Accounts.RemoveAll(a => a.Name == prof.Name);
                    _main.Cfg.Accounts.Add(new Core.Config.AccountInfo
                    {
                        Name = prof.Name,
                        Uuid = prof.Uuid,
                        AccessToken = prof.AccessToken,
                        AccountType = "littleskin",
                        AuthServer = "LittleSkin"
                    });
                    if (string.IsNullOrEmpty(_main.Cfg.ActiveAccountName))
                        _main.Cfg.ActiveAccountName = prof.Name;
                    _main.Cfg.Save();
                    RefreshList();
                    _main.SetStatus("外置账号 " + prof.Name + " 登录成功 ✓");
                    _extPass.Clear();
                }
                catch (Exception ex)
                {
                    _main.SetStatus("外置登录失败：" + ex.Message);
                    MessageBox.Show(ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            _rightCard.Controls.Add(extBtn);

            LayoutCards();
            RefreshList();
        }

        private void UseSelected()
        {
            var a = _list.Selected;
            if (a == null) return;
            _main.Cfg.ActiveAccountName = a.Name;
            _main.Cfg.Save();
            _main.SetStatus("当前账号已切换为 " + a.Name);
            RefreshList();
        }

        private void RefreshList()
        {
            _list.SetAccounts(_main.Cfg.Accounts, _main.Cfg.ActiveAccountName);
        }

        private async Task MicrosoftLogin()
        {
            var clientId = _clientId.Visible ? _clientId.Text.Trim() : "";
            if (clientId.Length == 0) clientId = _main.Cfg.MsClientId; // 留空 = 内置应用
            if (!Guid.TryParse(clientId, out _))
            {
                MessageBox.Show("Client ID 格式不对——应该是一串 GUID，形如\n" +
                                "00000000-0000-0000-0000-000000000000\n\n" +
                                "复制的是 Azure 应用概览里的「应用程序(客户端) ID」。", "Easyer");
                return;
            }
            _main.Cfg.MsClientId = clientId;
            _main.Cfg.Save();

            if (_deviceCodeWaiting) return;
            _deviceCodeWaiting = true;
            try
            {
                var dc = await Task.Run(() => Core.Auth.MicrosoftAuth.BeginDeviceCode(clientId));
                // 自动开浏览器 + 代码进剪贴板，少一步手动操作
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dc.VerificationUri) { UseShellExecute = true }); } catch { }
                try { Clipboard.SetText(dc.UserCode); } catch { }
                MessageBox.Show(
                    "1. 浏览器已打开（没开就手动访问）：\n    " + dc.VerificationUri + "\n\n" +
                    "2. 粘贴代码（已复制到剪贴板）：" + dc.UserCode + "\n\n" +
                    "点击「确定」后我会开始等待你完成授权（最多 5 分钟）……",
                    "Microsoft 登录", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _main.SetStatus("等待微软授权（设备码 " + dc.UserCode + "）...");

                Core.Auth.MicrosoftAuth.McProfile profile = null;
                await Task.Run(() =>
                {
                    for (int i = 0; i < 60; i++)
                    {
                        profile = Core.Auth.MicrosoftAuth.PollAndFinish(clientId, dc);
                        if (profile != null) return;
                        System.Threading.Thread.Sleep(dc.Interval * 1000);
                    }
                });

                if (profile == null)
                {
                    _main.SetStatus("授权超时。");
                    return;
                }

                _main.Cfg.Accounts.RemoveAll(a => a.Name == profile.Name);
                _main.Cfg.Accounts.Add(new Core.Config.AccountInfo
                {
                    Name = profile.Name,
                    Uuid = profile.Uuid,
                    AccessToken = profile.AccessToken,
                    MsRefreshToken = profile.RefreshToken,
                    MsExpiresAt = DateTime.UtcNow.AddHours(23).ToString("o"),
                    AccountType = "microsoft"
                });
                _main.Cfg.ActiveAccountName = profile.Name;
                _main.Cfg.Save();
                RefreshList();
                _main.SetStatus("正版账号 " + profile.Name + " 登录成功 ✓");
            }
            catch (Exception ex)
            {
                _main.SetStatus("微软登录失败：" + ex.Message);
                MessageBox.Show("登录失败：\n" + ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _deviceCodeWaiting = false;
            }
        }
    }

    // ================= 设置 =================
    public class SettingsPage : Panel
    {
        private readonly MainForm _main;
        private TextBox _gameDir, _customBg, _customCard, _customFg;
        private ComboBox _java, _themeMode;
        private NumericUpDown _mem, _w, _h;
        private CheckBox _fullscreen;
        private string _accentHex;

        public SettingsPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            AutoScroll = true;          // 三张卡加起来比窗口高，不开滚动下面的卡片就永远摸不到
            _accentHex = main.Cfg.AccentColor;
            Build();
        }

        private void Build()
        {
            var cfg = _main.Cfg;
            Controls.Add(PageUtil.Title("设置"));
            int x = 32, w1 = 700;

            // 游戏与 Java
            var card1 = new W11Card { Bounds = new Rectangle(x, 90, w1, 250) };
            Controls.Add(card1);
            card1.Controls.Add(Mk("游戏目录（默认 = 启动器同级的 .minecraft）", 24, 20));
            _gameDir = new TextBox { Bounds = new Rectangle(24, 48, 520, 32), BorderStyle = BorderStyle.FixedSingle, Text = cfg.GameDirectory };
            card1.Controls.Add(_gameDir);
            var browseBtn = new W11Button { Text = "浏览", Bounds = new Rectangle(556, 47, 90, 34) };
            browseBtn.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog { SelectedPath = _gameDir.Text })
                    if (fbd.ShowDialog() == DialogResult.OK) _gameDir.Text = fbd.SelectedPath;
            };
            card1.Controls.Add(browseBtn);

            card1.Controls.Add(Mk("Java（用于启动游戏与安装 Forge）", 24, 94));
            _java = new ComboBox { Bounds = new Rectangle(24, 122, 520, 32), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            var scanBtn = new W11Button { Text = "自动检测", Bounds = new Rectangle(556, 121, 90, 34) };
            scanBtn.Click += (s, e) => ScanJava();
            card1.Controls.Add(scanBtn);
            card1.Controls.Add(_java);
            var manual = new W11Button { Text = "手动指定 java.exe", Bounds = new Rectangle(24, 164, 200, 34) };
            manual.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog { Filter = "java.exe|java.exe" })
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _java.Items.Add(ofd.FileName);
                        _java.SelectedItem = ofd.FileName;
                    }
            };
            card1.Controls.Add(manual);
            card1.Controls.Add(Mk("内存 (MB)", 24, 210));
            _mem = new NumericUpDown { Bounds = new Rectangle(130, 206, 120, 30), Minimum = 1024, Maximum = 32768, Increment = 512, Value = cfg.MaxMemoryMB };
            card1.Controls.Add(_mem);
            card1.Controls.Add(Mk("分辨率", 300, 210));
            _w = new NumericUpDown { Bounds = new Rectangle(380, 206, 90, 30), Minimum = 640, Maximum = 7680, Value = cfg.WindowWidth };
            _h = new NumericUpDown { Bounds = new Rectangle(486, 206, 90, 30), Minimum = 480, Maximum = 4320, Value = cfg.WindowHeight };
            _fullscreen = new CheckBox { Text = "全屏", Bounds = new Rectangle(600, 206, 80, 30), Checked = cfg.FullScreen, BackColor = Color.Transparent };
            card1.Controls.Add(_w);
            card1.Controls.Add(_h);
            card1.Controls.Add(_fullscreen);

            // 主题
            var card2 = new W11Card { Bounds = new Rectangle(x, 360, w1, 330) };
            Controls.Add(card2);
            card2.Controls.Add(Mk("主题模式", 24, 20));
            _themeMode = new ComboBox { Bounds = new Rectangle(24, 48, 200, 32), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            _themeMode.Items.AddRange(new object[] { "深色（Win11）", "浅色（Win11）", "自定义" });
            _themeMode.SelectedIndex = cfg.ThemeMode == "light" ? 1 : cfg.ThemeMode == "custom" ? 2 : 0;
            card2.Controls.Add(_themeMode);

            var previewBtn = new W11Button { Text = "预览主题", Bounds = new Rectangle(244, 47, 120, 34) };
            previewBtn.Click += (s, e) => ApplyThemeNow();
            card2.Controls.Add(previewBtn);

            card2.Controls.Add(Mk("强调色预设", 24, 96));
            int px = 24;
            foreach (var (name, hex) in Theme.AccentPresets)
            {
                var hexLocal = hex;
                var b = new W11Button
                {
                    Text = name,
                    Bounds = new Rectangle(px, 124, 100, 34),
                    ButtonKind = hex == _accentHex ? W11Button.Kind.Accent : W11Button.Kind.Standard
                };
                b.Click += (s, e) => { _accentHex = hexLocal; ApplyThemeNow(); Build(); };
                card2.Controls.Add(b);
                px += 108;
            }

            card2.Controls.Add(Mk("自定义颜色（模式选「自定义」时生效）", 24, 176));
            _customBg = HexBox(card2, "背景", cfg.CustomBackground, 24, 208);
            _customCard = HexBox(card2, "卡片", cfg.CustomCard, 224, 208);
            _customFg = HexBox(card2, "文字", cfg.CustomForeground, 424, 208);

            var saveBtn = new W11Button
            {
                Text = "保存全部设置",
                IconGlyph = "check",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(24, 260, 220, 44),
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold)
            };
            saveBtn.Click += (s, e) => SaveAll();
            card2.Controls.Add(saveBtn);

            // ===== 更新与声明 =====
            var card3 = new W11Card { Bounds = new Rectangle(x, 710, w1, 150) };
            Controls.Add(card3);
            card3.Controls.Add(Mk("更新与声明", 24, 20));
            _autoUpdate = new CheckBox
            {
                Text = "启动时自动检查更新（GitHub Releases，不上传任何本地数据）",
                Bounds = new Rectangle(24, 46, 520, 30),
                Checked = cfg.AutoCheckUpdate,
                BackColor = Color.Transparent,
                ForeColor = Theme.Foreground
            };
            card3.Controls.Add(_autoUpdate);

            var checkBtn = new W11Button { Text = "检查更新", IconGlyph = "refresh", Bounds = new Rectangle(24, 86, 150, 38) };
            checkBtn.Click += (s, e) => _ = CheckUpdate();
            card3.Controls.Add(checkBtn);

            var aboutBtn = new W11Button { Text = "软件声明", IconGlyph = "info", Bounds = new Rectangle(188, 86, 150, 38) };
            aboutBtn.Click += (s, e) => { using var d = new AboutDialog(false); d.ShowDialog(FindForm()); };
            card3.Controls.Add(aboutBtn);

            var verTip = new Label
            {
                Text = "当前版本 " + AppInfo.Display + "  ·  " + AppInfo.RepoUrl,
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(356, 96)
            };
            card3.Controls.Add(verTip);
        }

        private CheckBox _autoUpdate;

        private async System.Threading.Tasks.Task CheckUpdate()
        {
            _main.SetStatus("正在检查更新 ...");
            _main.BeginTask("正在检查更新");
            try
            {
                var rel = await Updater.CheckAsync();
                _main.EndTask();
                if (rel == null)
                {
                    _main.SetStatus("已经是最新版 " + AppInfo.Display);
                    MessageBox.Show("当前已是最新版（" + AppInfo.Display + "）。", "Easyer",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using var d = new UpdateDialog(rel);
                d.ShowDialog(FindForm());
            }
            catch (Exception ex)
            {
                _main.EndTask();
                _main.SetStatus("检查更新失败：" + ex.Message);
                MessageBox.Show("检查更新失败：\n" + ex.Message + "\n\n可以直接去 GitHub Releases 看：\n" + AppInfo.ReleasesUrl,
                    "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private TextBox HexBox(Control parent, string label, string value, int x, int y)
        {
            parent.Controls.Add(new Label { Text = label, AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.ForegroundDim, Location = new Point(x, y) });
            var tb = new TextBox { Bounds = new Rectangle(x, y + 22, 170, 30), BorderStyle = BorderStyle.FixedSingle, Text = value };
            parent.Controls.Add(tb);
            return tb;
        }

        private static Label Mk(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                BackColor = Color.Transparent,
                ForeColor = Theme.ForegroundDim,
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private void ScanJava()
        {
            _java.Items.Clear();
            _main.SetStatus("正在扫描本机 Java ...");
            Task.Run(() =>
            {
                var list = JavaLocator.FindAll();
                BeginInvoke(new Action(() =>
                {
                    foreach (var j in list)
                    {
                        _java.Items.Add(j.JavaExe);
                        // 简单把版本挂上 tooltip
                    }
                    if (_java.Items.Count > 0) _java.SelectedIndex = 0;
                    _main.SetStatus("检测到 " + list.Count + " 个 Java 安装");
                }));
            });
        }

        private void ApplyThemeNow()
        {
            string mode = _themeMode.SelectedIndex switch
            {
                1 => "light",
                2 => "custom",
                _ => "dark"
            };
            Theme.Apply(mode, _accentHex, _customBg.Text, _customCard.Text, _customFg.Text);
        }

        private void SaveAll()
        {
            var cfg = _main.Cfg;
            cfg.GameDirectory = _gameDir.Text.Trim();
            if (_java.SelectedItem != null) cfg.JavaPath = _java.SelectedItem.ToString();
            cfg.MaxMemoryMB = (int)_mem.Value;
            cfg.WindowWidth = (int)_w.Value;
            cfg.WindowHeight = (int)_h.Value;
            cfg.FullScreen = _fullscreen.Checked;
            cfg.ThemeMode = _themeMode.SelectedIndex switch { 1 => "light", 2 => "custom", _ => "dark" };
            cfg.AccentColor = _accentHex;
            cfg.CustomBackground = _customBg.Text;
            cfg.CustomCard = _customCard.Text;
            cfg.CustomForeground = _customFg.Text;
            cfg.AutoCheckUpdate = _autoUpdate != null && _autoUpdate.Checked;
            cfg.Save();
            ApplyThemeNow();
            _main.SetStatus("设置已保存 ✓");
        }
    }
}
