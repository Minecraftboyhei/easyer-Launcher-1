using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;
using EasyerLauncher.Core.SystemHelpers;

namespace EasyerLauncher.Pages
{
    /// <summary>主页：大启动卡 + 信息卡 + 日志。</summary>
    public class HomePage : Panel
    {
        private readonly MainForm _main;
        private ComboBox _versionCombo;
        private Label _accountLabel;

        public HomePage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();
        }

        private W11Card _launchCard, _logCard;
        private W11Card[] _infoCards;
        private TextBox _logBox;

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Relayout();
        }

        private void Relayout()
        {
            if (_launchCard == null) return;
            int w = Math.Max(700, Width - 64);
            _launchCard.Bounds = new Rectangle(32, 90, w, 300);
            int gap = 16;
            int cw = (w - gap * 2) / 3;
            for (int i = 0; i < 3; i++)
                _infoCards[i].Bounds = new Rectangle(32 + i * (cw + gap), 410, cw, 130);
            _logCard.Bounds = new Rectangle(32, 560, w, Math.Max(120, Height - 590));
            _logBox.Bounds = new Rectangle(16, 14, w - 32, _logCard.Height - 28);
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("主页"));

            // ===== 启动大卡 =====
            _launchCard = new W11Card { Bounds = new Rectangle(32, 90, 976, 300) };

            var banner = new Label
            {
                Text = "准备就绪，随时开坑",
                Font = new Font(Theme.DefaultFontFamily, 17f, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = Theme.Foreground,
                Location = new Point(32, 28),
                AutoSize = true
            };
            _launchCard.Controls.Add(banner);

            _launchCard.Controls.Add(new Label
            {
                Text = "游戏版本",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(32, 76),
                AutoSize = true
            });
            _versionCombo = new ComboBox
            {
                Bounds = new Rectangle(32, 100, 400, 38),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.BodyFont
            };
            _launchCard.Controls.Add(_versionCombo);

            var refreshBtn = new W11Button
            {
                Text = "刷新",
                IconGlyph = "\uE72C",  // Refresh
                Bounds = new Rectangle(448, 101, 96, 36)
            };
            refreshBtn.Click += (s, e) => LoadVersions();
            _launchCard.Controls.Add(refreshBtn);

            _accountLabel = new Label
            {
                BackColor = Color.Transparent,
                Font = Theme.BodyFont,
                Location = new Point(32, 152),
                AutoSize = true
            };
            _launchCard.Controls.Add(_accountLabel);

            var launchBtn = new W11Button
            {
                Text = "启动游戏",
                IconGlyph = "\uE768",  // Play
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 13f, FontStyle.Bold),
                Bounds = new Rectangle(32, 190, 280, 60)
            };
            launchBtn.Click += (s, e) =>
            {
                if (_versionCombo.SelectedItem is string v && !v.StartsWith("（"))
                    _main.LaunchGame(v);
                else
                {
                    MessageBox.Show("还没装任何版本！点右边「去下载」挑一个。", "Easyer");
                    _main.Navigate("download");
                }
            };
            _launchCard.Controls.Add(launchBtn);

            var dlBtn = new W11Button
            {
                Text = "去下载",
                IconGlyph = "\uE896",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(328, 196, 150, 48)
            };
            dlBtn.Click += (s, e) => _main.Navigate("download");
            _launchCard.Controls.Add(dlBtn);

            var serverBtn = new W11Button
            {
                Text = "启动器设置",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(492, 196, 150, 48)
            };
            serverBtn.Click += (s, e) => _main.Navigate("settings");
            _launchCard.Controls.Add(serverBtn);

            Controls.Add(_launchCard);

            // ===== 信息卡 × 3 =====
            string[] infos = InfoLines();
            var cardDefs = new (string Title, string Value, string Glyph, string Btn, Action Click)[]
            {
                ("游戏目录", Truncate(infos[0], 40), "\uE8B7", "打开目录",
                    () => { try { System.Diagnostics.Process.Start("explorer.exe", _main.Cfg.GameDirectory); } catch { } }),
                ("Java 环境", infos[1], "\uE943", "检测详情",
                    () => _main.Navigate("settings")),
                ("内存分配", infos[2], "\uE950", "调整",
                    () => _main.Navigate("settings")),
            };
            _infoCards = new W11Card[3];
            int ix = 32;
            for (int i = 0; i < 3; i++)
            {
                var def = cardDefs[i];
                var card = new W11Card { Bounds = new Rectangle(ix, 410, 314, 130) };
                _infoCards[i] = card;
                Controls.Add(card);

                var icon = new Label
                {
                    Text = def.Glyph,
                    Font = Theme.BigIconFont,
                    ForeColor = Theme.Accent,
                    BackColor = Color.Transparent,
                    Location = new Point(22, 18),
                    AutoSize = true
                };
                card.Controls.Add(icon);
                card.Controls.Add(new Label
                {
                    Text = def.Title,
                    Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                    BackColor = Color.Transparent,
                    ForeColor = Theme.Foreground,
                    Location = new Point(72, 22),
                    AutoSize = true
                });
                card.Controls.Add(new Label
                {
                    Text = def.Value,
                    Font = Theme.SmallFont,
                    BackColor = Color.Transparent,
                    ForeColor = Theme.ForegroundDim,
                    Location = new Point(22, 66),
                    AutoSize = false,
                    Size = new Size(272, 18)
                });
                var btn = new W11Button
                {
                    Text = def.Btn,
                    ButtonKind = W11Button.Kind.Standard,
                    Bounds = new Rectangle(22, 90, 110, 30),
                    Font = Theme.SmallFont
                };
                var click = def.Click;
                btn.Click += (s, e) => click();
                card.Controls.Add(btn);
                ix += 331;
            }

            // ===== 日志 =====
            _logCard = new W11Card { Bounds = new Rectangle(32, 560, 976, 150) };
            Controls.Add(_logCard);
            _logBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Theme.Input,
                ForeColor = Theme.ForegroundDim,
                BorderStyle = BorderStyle.None,
                Font = Theme.SmallFont,
                Bounds = new Rectangle(16, 14, 944, 122),
                Text = "Easyer Launcher 1 — 日志\r\n"
            };
            _logCard.Controls.Add(_logBox);

            LoadVersions();
            Relayout();
        }

        private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";

        private string[] InfoLines()
        {
            var javaVer = "未检测";
            try
            {
                var j = JavaLocator.FindAll().FirstOrDefault();
                if (j != null) javaVer = Truncate(j.VersionString, 36);
            }
            catch { }
            return new[]
            {
                _main.Cfg.GameDirectory,
                javaVer,
                _main.Cfg.MaxMemoryMB + " MB"
            };
        }

        private void LoadVersions()
        {
            _versionCombo.Items.Clear();
            var list = GameInstaller.ListInstalledVersions(_main.Cfg.GameDirectory);
            if (list.Count == 0)
            {
                _versionCombo.Items.Add("（还没装任何版本，点「去下载」）");
                _versionCombo.SelectedIndex = 0;
            }
            else
            {
                foreach (var v in list) _versionCombo.Items.Add(v);
                _versionCombo.SelectedIndex = 0;
            }
            var acc = _main.Cfg.GetActiveAccount();
            _accountLabel.Text = "当前账号：" + (acc == null
                ? "（未设置，去「账号」页添加，离线账号秒建）"
                : acc.Name + (acc.AccountType == "microsoft" ? "  [正版]" : "  [离线]"));
            _accountLabel.ForeColor = acc == null ? Theme.ForegroundDim : Theme.Foreground;
        }

        public void AppendLog(string msg)
        {
            if (_logBox.InvokeRequired) { _logBox.BeginInvoke(new Action(() => _logBox.AppendText(msg + "\r\n"))); return; }
            _logBox.AppendText(msg + "\r\n");
        }
    }
}
