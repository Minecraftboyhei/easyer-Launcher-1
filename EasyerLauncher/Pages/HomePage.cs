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
        private CardPicker _versionPick;
        private Label _accountLabel;
        private AvatarBox _avatar;
        private ProgressRing _taskRing;

        public HomePage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();
            _main.TaskState += OnTaskState;
            Disposed += (s, e) => _main.TaskState -= OnTaskState;
        }

        private void OnTaskState(string caption, float pct, bool running)
        {
            if (_taskRing == null || _taskRing.IsDisposed) return;
            if (running) _taskRing.Start(caption, pct);
            else _taskRing.Stop();
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
            if (_taskRing != null) _taskRing.Bounds = new Rectangle(w - 260, 34, 228, 24);
            if (_accountLabel != null) _accountLabel.Width = Math.Max(260, w - 120);
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

            _taskRing = new ProgressRing { Bounds = new Rectangle(716, 34, 228, 24), Visible = false };
            _launchCard.Controls.Add(_taskRing);

            _launchCard.Controls.Add(new Label
            {
                Text = "游戏版本",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(32, 76),
                AutoSize = true
            });
            _versionPick = new CardPicker
            {
                Bounds = new Rectangle(32, 100, 700, 68),
                CardSize = new Size(168, 60)
            };
            _launchCard.Controls.Add(_versionPick);

            var refreshBtn = new W11Button
            {
                Text = "刷新",
                IconGlyph = "refresh",
                Bounds = new Rectangle(748, 116, 96, 36)
            };
            refreshBtn.Click += (s, e) => LoadVersions();
            _launchCard.Controls.Add(refreshBtn);

            _avatar = new AvatarBox { Location = new Point(32, 170), Size = new Size(40, 40) };
            _launchCard.Controls.Add(_avatar);

            // 定宽 + 省略号：昵称再长也不会压到下面的按钮
            _accountLabel = new Label
            {
                BackColor = Color.Transparent,
                Font = Theme.BodyFont,
                Location = new Point(82, 180),
                AutoSize = false,
                AutoEllipsis = true,
                Size = new Size(440, 22)
            };
            _launchCard.Controls.Add(_accountLabel);

            var launchBtn = new W11Button
            {
                Text = "启动游戏",
                IconGlyph = "play",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 13f, FontStyle.Bold),
                Bounds = new Rectangle(32, 216, 280, 56)
            };
            launchBtn.Click += (s, e) =>
            {
                var sel = _versionPick?.SelectedItem;
                if (sel != null && !sel.Title.StartsWith("（"))
                {
                    var id = sel.Tag as string;
                    _main.LaunchGame(string.IsNullOrEmpty(id) ? sel.Title : id);
                }
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
                IconGlyph = "download",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(328, 214, 150, 48)
            };
            dlBtn.Click += (s, e) => _main.Navigate("download");
            _launchCard.Controls.Add(dlBtn);

            var serverBtn = new W11Button
            {
                Text = "启动器设置",
                IconGlyph = "settings",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(492, 214, 150, 48)
            };
            serverBtn.Click += (s, e) => _main.Navigate("settings");
            _launchCard.Controls.Add(serverBtn);

            Controls.Add(_launchCard);

            // ===== 信息卡 × 3 =====
            string[] infos = InfoLines();
            var cardDefs = new (string Title, string Value, string Glyph, string Btn, Action Click)[]
            {
                ("游戏目录", Truncate(infos[0], 40), "folder", "打开目录",
                    () => { try { System.Diagnostics.Process.Start("explorer.exe", _main.Cfg.GameDirectory); } catch { } }),
                ("Java 环境", infos[1], "shield", "检测详情",
                    () => _main.Navigate("settings")),
                ("内存分配", infos[2], "memory", "调整",
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

                card.Controls.Add(new IconBox
                {
                    IconName = def.Glyph,
                    Location = new Point(20, 16),
                    Size = new Size(32, 32)
                });
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
            var items = new System.Collections.Generic.List<CardPicker.Item>();

            // 整合包实例优先展示（显示整合包名，Tag 存版本 id）
            foreach (var inst in _main.Cfg.Instances)
            {
                if (!Directory.Exists(inst.Dir)) continue;
                items.Add(new CardPicker.Item
                {
                    Title = inst.Name,
                    Sub = (inst.GameVersion ?? "") + (string.IsNullOrEmpty(inst.Loader) ? "" : "  ·  " + inst.Loader),
                    Badge = "整合包",
                    IconKind = "pack",
                    Tag = inst.Id
                });
            }

            var list = GameInstaller.ListInstalledVersions(_main.Cfg.GameDirectory);
            foreach (var v in list)
            {
                if (v.StartsWith("pack-", StringComparison.OrdinalIgnoreCase)) continue;  // 实例已单独展示
                items.Add(new CardPicker.Item { Title = v, Sub = "标准版本", IconKind = "vanilla", Tag = v });
            }

            if (items.Count == 0)
                items.Add(new CardPicker.Item { Title = "（还没装任何版本）", Sub = "点「去下载」挑一个", Tag = "" });

            _versionPick.SetItems(items);
            var acc = _main.Cfg.GetActiveAccount();
            _avatar.Bind(acc);
            _accountLabel.Text = "当前账号：" + (acc == null
                ? "（未设置，去「账号」页添加，离线账号秒建）"
                : acc.Name + (acc.AccountType == "microsoft" ? "  [正版]"
                            : acc.AccountType == "littleskin" ? "  [外置]" : "  [离线]"));
            _accountLabel.ForeColor = acc == null ? Theme.ForegroundDim : Theme.Foreground;
        }

        public void AppendLog(string msg)
        {
            if (_logBox.InvokeRequired) { _logBox.BeginInvoke(new Action(() => _logBox.AppendText(msg + "\r\n"))); return; }
            _logBox.AppendText(msg + "\r\n");
        }
    }
}
