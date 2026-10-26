using System;
using System.Collections.Generic;
using System.Drawing;
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

    // ================= 主页 =================
    public class AccountPage : Panel
    {
        private readonly MainForm _main;
        private ListBox _list;
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
            _list = new ListBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme.BodyFont,
                Bounds = new Rectangle(16, 16, 528, 220),
                IntegralHeight = false,
                BackColor = Theme.Input,
                ForeColor = Theme.Foreground
            };
            _leftCard.Controls.Add(_list);

            var useBtn = new W11Button { Text = "切换为当前账号", Bounds = new Rectangle(16, 250, 190, 38) };
            useBtn.Click += (s, e) =>
            {
                if (_list.SelectedIndex < 0) return;
                var a = _main.Cfg.Accounts[_list.SelectedIndex];
                _main.Cfg.ActiveAccountName = a.Name;
                _main.Cfg.Save();
                _main.SetStatus("当前账号已切换为 " + a.Name);
                RefreshList();
            };
            _leftCard.Controls.Add(useBtn);

            var delBtn = new W11Button { Text = "删除账号", Bounds = new Rectangle(216, 250, 130, 38) };
            delBtn.Click += (s, e) =>
            {
                if (_list.SelectedIndex < 0) return;
                var a = _main.Cfg.Accounts[_list.SelectedIndex];
                if (MessageBox.Show("删除账号 " + a.Name + "？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _main.Cfg.Accounts.RemoveAt(_list.SelectedIndex);
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
                Text = "已内置登录应用，账号页开箱即用。\n" +
                       "注意：Mojang 对第三方启动器实行 API 白名单——\n" +
                       "自建 Client ID 需到 aka.ms/mce-reviewappid 提交审核\n" +
                       "（几天到几周）。审核没过之前，正版登录会在最后\n" +
                       "一步被 Mojang 拦下；现在就能玩：LittleSkin 外置 / 离线登录。",
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
                Text = _main.Cfg.MsClientId,
                PlaceholderText = "Azure 应用 Client ID（GUID）"
            };
            _rightCard.Controls.Add(_clientId);

            var azureBtn = new W11Button { Text = "去注册", Bounds = new Rectangle(330, 322, 94, 30) };
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

            var msBtn = new W11Button { Text = "浏览器登录（推荐）", ButtonKind = W11Button.Kind.Accent, Bounds = new Rectangle(20, 364, 190, 40) };
            var dcBtn = new W11Button { Text = "设备码登录", ButtonKind = W11Button.Kind.Ghost, Bounds = new Rectangle(220, 366, 130, 38) };
            dcBtn.Click += (s, e) => _ = MicrosoftLogin();
            msBtn.Click += async (s, e) =>
            {
                var clientId = _clientId.Text.Trim();
                if (!Guid.TryParse(clientId, out _))
                {
                    MessageBox.Show("先填一个有效的 Client ID（GUID）！\n" +
                        "通常已经自动填好了，不要清空它。", "Easyer");
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

        private void RefreshList()
        {
            _list.Items.Clear();
            foreach (var a in _main.Cfg.Accounts)
                _list.Items.Add(a.Name + (a.Name == _main.Cfg.ActiveAccountName ? "   ← 当前" : "") +
                                (a.AccountType == "microsoft" ? "   [正版]" : a.AccountType == "littleskin" ? "   [外置]" : "   [离线]"));
        }

        private async Task MicrosoftLogin()
        {
            var clientId = _clientId.Text.Trim();
            if (clientId.Length == 0)
            {
                MessageBox.Show("先填入 Azure 应用的 Client ID（点右边「去注册」两分钟搞定）。", "Easyer");
                return;
            }
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
            card1.Controls.Add(Mk("游戏目录", 24, 20));
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
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(24, 260, 220, 44),
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold)
            };
            saveBtn.Click += (s, e) => SaveAll();
            card2.Controls.Add(saveBtn);
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
            cfg.Save();
            ApplyThemeNow();
            _main.SetStatus("设置已保存 ✓");
        }
    }
}
