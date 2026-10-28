using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;

namespace EasyerLauncher.Pages
{
    /// <summary>安装选项弹窗（PCL 式）：下载版本时直接选加载器，一步到位。</summary>
    internal class InstallPickForm : Form
    {
        private readonly MainForm _main;
        private readonly string _gameId;
        private readonly InstallOptionList _list;
        private readonly W11Button _nextBtn;

        private static readonly (string Key, string Title, string Sub)[] Options =
        {
            ("vanilla",  "原版",            "纯净安装，不装任何加载器"),
            ("fabric",   "Fabric",          "轻量主流，新模组首选"),
            ("quilt",    "Quilt",           "Fabric 分支，兼容 Fabric 模组"),
            ("neoforge", "NeoForge",        "Forge 新锐分支（1.20.1+）"),
            ("forge",    "Forge",           "老牌大哥，历史模组都在这"),
        };

        public InstallPickForm(MainForm main, string gameId)
        {
            _main = main;
            _gameId = gameId;

            Text = "安装 " + gameId + " — 选择加载器";
            Size = new Size(560, 470);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var title = new Label
            {
                Text = "要安装 " + gameId + "，选哪种玩法？",
                Font = new Font(Theme.DefaultFontFamily, 12.5f, FontStyle.Bold),
                Bounds = new Rectangle(24, 18, 500, 30)
            };
            Controls.Add(title);

            _list = new InstallOptionList { Bounds = new Rectangle(24, 60, 500, 250) };
            _list.SetGame(gameId);
            _list.OnConfirm = Next;   // 双击行直接开始
            Controls.Add(_list);

            _nextBtn = new W11Button
            {
                Text = "下一步",
                IconGlyph = "next",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(24, 330, 200, 46)
            };
            _nextBtn.Click += (s, e) => Next();
            Controls.Add(_nextBtn);

            var cancel = new W11Button
            {
                Text = "取消",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(244, 332, 110, 42)
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            var tip = new Label
            {
                Text = "版本可用性正在检测 ...",
                Name = "availTip",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                Bounds = new Rectangle(24, 390, 500, 20)
            };
            Controls.Add(tip);

            Theme.ApplyTo(this);
            Theme.EnableWin11Window(this);
            _ = CheckAvailability();
        }

        private async Task CheckAvailability()
        {
            var tip = Controls.Find("availTip", true).FirstOrDefault() as Label;
            try
            {
                var counts = await Task.Run(() =>
                {
                    var d = new Dictionary<string, int>();
                    d["fabric"] = FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Fabric, _gameId).Count;
                    d["quilt"] = FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Quilt, _gameId).Count;
                    d["forge"] = ForgeInstaller.GetVersions(_gameId).Count;
                    d["neoforge"] = NeoForgeInstaller.IsSupported(_gameId)
                        ? NeoForgeInstaller.GetVersions(_gameId).Count : -1;
                    return d;
                });
                _list.SetCounts(counts);
                if (tip != null)
                {
                    int ok = counts.Count(kv => kv.Value > 0);
                    tip.Text = "原版可直接安装；加载器选中后点「下一步」或直接双击该行";
                }
            }
            catch (Exception ex)
            {
                if (tip != null) tip.Text = "可用性检测失败（网络问题），仍可直接选「原版」或稍后再试";
                _main.SetStatus("加载器可用性检测失败：" + ex.Message);
            }
        }

        private void Next()
        {
            var key = _list.SelectedKey;
            if (key == null)
            {
                MessageBox.Show("先在列表里选一种安装方式！", "Easyer");
                return;
            }
            if (key == "vanilla")
            {
                Hide();
                Installer.BeginVanilla(_main, _gameId);
                Close();
                return;
            }

            // 加载器：先弹版本列表
            try
            {
                Cursor = Cursors.WaitCursor;
                List<string> versions = key switch
                {
                    "fabric" => FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Fabric, _gameId),
                    "quilt" => FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Quilt, _gameId),
                    "forge" => ForgeInstaller.GetVersions(_gameId).Select(f => f.Build).ToList(),
                    _ => NeoForgeInstaller.GetVersions(_gameId),
                };
                Cursor = Cursors.Default;
                if (versions.Count == 0)
                {
                    MessageBox.Show("该版本没有可用的 " + key + "，换个游戏版本试试。", "Easyer");
                    return;
                }
                Hide();
                using (var vdlg = new LoaderVersionPickForm(_main, _gameId, key, versions))
                    vdlg.ShowDialog(FindForm());
                Close();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("获取加载器版本失败：\n" + ex.Message, "Easyer",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>安装方式列表（自绘行）。</summary>
    internal class InstallOptionList : Control
    {
        private readonly int[] _counts = { -2, -2, -2, -2, -2 };  // -2 检测中, -1 不支持, ≥0 可用数
        private int _selectedIndex = 0;
        private string _gameId = "";
        public const int RowH = 46;

        public string SelectedKey => _selectedIndex >= 0 ? Options[_selectedIndex].Key : null;
        public Action OnConfirm;  // 双击行 = 直接确认

        private static readonly (string Key, string Title, string Sub)[] Options =
        {
            ("vanilla",  "原版",   "纯净安装，不装任何加载器"),
            ("fabric",   "Fabric", "轻量主流，新模组首选"),
            ("quilt",    "Quilt",  "Fabric 分支，兼容 Fabric 模组"),
            ("neoforge", "NeoForge", "Forge 新锐分支（1.20.1+）"),
            ("forge",    "Forge",  "老牌大哥，历史模组都在这"),
        };

        /// <summary>原版永远可用；加载器行看检测结果。</summary>
        private bool Available(int i) => i == 0 || _counts[i] >= 0;

        public InstallOptionList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
        }

        public void SetGame(string gameId) { _gameId = gameId; Invalidate(); }

        public void SetCounts(Dictionary<string, int> counts)
        {
            for (int i = 0; i < Options.Length; i++)
                if (counts.TryGetValue(Options[i].Key, out var c)) _counts[i] = c;
            // 不支持的行跳过选中
            if (_counts[_selectedIndex] < 0)
            {
                int first = Array.FindIndex(_counts, c => c > 0);
                if (first < 0) first = 0;
                _selectedIndex = first;
            }
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int row = e.Y / RowH;
            if (row >= 0 && row < Options.Length && Available(row))
            {
                _selectedIndex = row;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int row = e.Y / RowH;
            if (row >= 0 && row < Options.Length && Available(row))
            {
                _selectedIndex = row;
                Invalidate();
                OnConfirm?.Invoke();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            for (int i = 0; i < Options.Length; i++)
            {
                var rect = new Rectangle(0, i * RowH + 2, Width - 1, RowH - 4);
                bool sel = i == _selectedIndex;
                bool disabled = !Available(i);

                using (var path = Theme.RoundedPath(rect, 10))
                {
                    using (var b = new SolidBrush(sel ? Color.FromArgb(50, Theme.Accent) : Theme.Card))
                        g.FillPath(b, path);
                    using (var pen = new Pen(sel ? Theme.Accent : Theme.Border))
                        g.DrawPath(pen, path);
                }

                var titleColor = disabled ? Theme.ForegroundDim : sel ? Theme.Accent : Theme.Foreground;
                TextRenderer.DrawText(g, Options[i].Title,
                    new Font(Theme.DefaultFontFamily, 10.5f, FontStyle.Bold),
                    new Rectangle(rect.X + 16, rect.Y + 4, 130, 20), titleColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, Options[i].Sub, Theme.SmallFont,
                    new Rectangle(rect.X + 16, rect.Y + 24, 340, 18), Theme.ForegroundDim,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // 右侧状态（原版永远就绪）
                string state;
                Color stateColor;
                if (i == 0)
                {
                    state = "✓ 可直接安装";
                    stateColor = Color.FromArgb(52, 211, 153);
                }
                else
                {
                    state = _counts[i] switch
                    {
                        -2 => "检测中 …",
                        -1 => "不支持",
                        0 => "无可用版本",
                        _ => "✓ " + _counts[i] + " 个版本",
                    };
                    stateColor = _counts[i] > 0 ? Color.FromArgb(52, 211, 153) : Theme.ForegroundDim;
                }
                TextRenderer.DrawText(g, state, Theme.SmallFont,
                    new Rectangle(rect.Right - 130, rect.Y + 4, 118, 20), stateColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }
    }

    /// <summary>加载器版本选择弹窗（图形卡片，单击选中）。</summary>
    internal class LoaderVersionPickForm : Form
    {
        private readonly MainForm _main;
        private readonly string _gameId, _loaderKey;
        private readonly List<string> _versions;
        private readonly CardPicker _picker;

        public LoaderVersionPickForm(MainForm main, string gameId, string loaderKey, List<string> versions)
        {
            _main = main;
            _gameId = gameId;
            _loaderKey = loaderKey;
            _versions = versions;

            Text = loaderKey + " 版本 — " + gameId;
            Size = new Size(620, 560);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var title = new Label
            {
                Text = "选一个 " + loaderKey + " 版本（第一个通常是最新版）",
                Font = new Font(Theme.DefaultFontFamily, 12f, FontStyle.Bold),
                ForeColor = Theme.Foreground,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(24, 18, 560, 30)
            };
            Controls.Add(title);

            _picker = new CardPicker
            {
                Bounds = new Rectangle(24, 56, 560, 400),
                CardSize = new Size(176, 58)
            };
            _picker.SetItems(versions.Select((v, i) => new CardPicker.Item
            {
                Title = v,
                Sub = _loaderKey,
                Badge = i == 0 ? "最新" : ""
            }));
            Controls.Add(_picker);

            var ok = new W11Button
            {
                Text = "开始安装",
                IconGlyph = "install",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(24, 464, 200, 46)
            };
            ok.Click += (s, e) => { Go(); Close(); };
            Controls.Add(ok);

            var cancel = new W11Button
            {
                Text = "取消",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(244, 466, 110, 42)
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            Theme.ApplyTo(this);
            Theme.EnableWin11Window(this);
        }

        private void Go()
        {
            var v = _picker.SelectedItem?.Title;
            if (string.IsNullOrEmpty(v)) { MessageBox.Show("先选一个版本！", "Easyer"); return; }
            Installer.BeginWithLoader(_main, _gameId, _loaderKey, v);
        }
    }

    /// <summary>后台安装流程（vanilla → loader）。</summary>
    internal static class Installer
    {
        public static void BeginVanilla(MainForm main, string gameId)
            => Run(main, gameId, null, null);

        public static void BeginWithLoader(MainForm main, string gameId, string loaderKey, string loaderVersion)
            => Run(main, gameId, loaderKey, loaderVersion);

        private static void Run(MainForm main, string gameId, string loaderKey, string loaderVersion)
        {
            var label = gameId + (loaderKey != null ? " + " + loaderKey + " " + loaderVersion : "");
            main.SetStatus("开始安装 " + label + " ...");
            main.BeginTask("安装 " + label);
            Task.Run(() =>
            {
                try
                {
                    // 1) 原版（加载器安装依赖原版）
                    GameInstaller.Install(gameId, main.Cfg.GameDirectory,
                        msg => main.ReportProgress("[" + gameId + "] " + msg));

                    // 2) 加载器
                    if (loaderKey != null)
                    {
                        switch (loaderKey)
                        {
                            case "fabric":
                                FabricInstaller.Install(FabricInstaller.LoaderKind.Fabric, gameId, loaderVersion,
                                    main.Cfg.GameDirectory, msg => main.ReportProgress("[Fabric] " + msg));
                                break;
                            case "quilt":
                                FabricInstaller.Install(FabricInstaller.LoaderKind.Quilt, gameId, loaderVersion,
                                    main.Cfg.GameDirectory, msg => main.ReportProgress("[Quilt] " + msg));
                                break;
                            case "forge":
                                ForgeInstaller.Install(gameId, loaderVersion, main.Cfg.GameDirectory,
                                    main.Cfg.JavaPath, msg => main.ReportProgress("[Forge] " + msg));
                                break;
                            case "neoforge":
                                NeoForgeInstaller.Install(gameId, loaderVersion, main.Cfg.GameDirectory,
                                    main.Cfg.JavaPath, msg => main.ReportProgress("[NeoForge] " + msg));
                                break;
                        }
                    }

                    main.SetStatus(label + " 安装完成 ✓");
                    main.RefreshHomeVersions();
                    main.EndTask();
                    main.BeginInvoke(new Action(() =>
                        MessageBox.Show(label + " 安装完成！主页版本列表已刷新。", "Easyer",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)));
                }
                catch (Exception ex)
                {
                    main.SetStatus("安装失败：" + ex.Message);
                    main.EndTask();
                    main.BeginInvoke(new Action(() =>
                        MessageBox.Show("安装失败：\n" + ex.Message, "Easyer",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }
    }
}
