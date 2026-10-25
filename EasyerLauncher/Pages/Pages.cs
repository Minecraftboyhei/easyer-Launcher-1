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
        private bool _deviceCodeWaiting = false;

        public AccountPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("账号"));

            // 账号列表
            var card = new W11Card { Bounds = new Rectangle(32, 90, 560, 360) };
            Controls.Add(card);
            _list = new ListBox
            {
                BorderStyle = BorderStyle.None,
                Font = Theme.BodyFont,
                Bounds = new Rectangle(16, 16, 528, 220),
                IntegralHeight = false
            };
            card.Controls.Add(_list);

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
            card.Controls.Add(useBtn);

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
            card.Controls.Add(delBtn);

            // 离线登录
            var offlineCard = new W11Card { Bounds = new Rectangle(612, 90, 480, 360) };
            Controls.Add(offlineCard);
            offlineCard.Controls.Add(new Label
            {
                Text = "离线账号（不需要正版）",
                Font = Theme.H2Font,
                BackColor = Color.Transparent,
                Location = new Point(20, 16),
                AutoSize = true
            });
            offlineCard.Controls.Add(new Label
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
            offlineCard.Controls.Add(_offlineName);

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
            offlineCard.Controls.Add(offlineBtn);

            // 正版登录
            offlineCard.Controls.Add(new Label
            {
                Text = "Microsoft 正版登录",
                Font = Theme.H2Font,
                BackColor = Color.Transparent,
                Location = new Point(20, 196),
                AutoSize = true
            });
            _clientId = new TextBox
            {
                Bounds = new Rectangle(20, 236, 300, 30),
                BorderStyle = BorderStyle.FixedSingle,
                Text = _main.Cfg.MsClientId,
                PlaceholderText = "Azure 应用的 Client ID（GUID）"
            };
            offlineCard.Controls.Add(_clientId);
            offlineCard.Controls.Add(new Label
            {
                Text = "需要自己在 Azure 注册一个应用（免费）：azure.portal → 应用注册 →\r\n受支持账户类型选「个人」→ 平台加「移动应用程序」→ 复制客户端 ID。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(20, 272),
                AutoSize = true
            });
            var msBtn = new W11Button { Text = "设备码登录", Bounds = new Rectangle(20, 316, 160, 38) };
            msBtn.Click += (s, e) => _ = MicrosoftLogin();
            offlineCard.Controls.Add(msBtn);

            RefreshList();
        }

        private void RefreshList()
        {
            _list.Items.Clear();
            foreach (var a in _main.Cfg.Accounts)
                _list.Items.Add(a.Name + (a.Name == _main.Cfg.ActiveAccountName ? "   ← 当前" : "") +
                                (a.AccountType == "microsoft" ? "   [正版]" : "   [离线]"));
        }

        private async Task MicrosoftLogin()
        {
            var clientId = _clientId.Text.Trim();
            if (clientId.Length == 0)
            {
                MessageBox.Show("先填入 Azure 应用的 Client ID！", "Easyer");
                return;
            }
            _main.Cfg.MsClientId = clientId;
            _main.Cfg.Save();

            if (_deviceCodeWaiting) return;
            _deviceCodeWaiting = true;
            try
            {
                var dc = await Task.Run(() => Core.Auth.MicrosoftAuth.BeginDeviceCode(clientId));
                MessageBox.Show(
                    "1. 打开浏览器访问：\n    " + dc.VerificationUri + "\n\n" +
                    "2. 输入代码：" + dc.UserCode + "\n\n" +
                    "点击「确定」后我会开始等待你完成授权……",
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
