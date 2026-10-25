using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;

namespace EasyerLauncher.Pages
{
    /// <summary>加载器：三大图形化加载器卡片 + 选中后展开安装面板。</summary>
    public class LoadersPage : Panel
    {
        private readonly MainForm _main;
        private int _selectedLoader = 0;   // 0 Fabric 1 Quilt 2 Forge
        private W11Card[] _loaderCards;
        private Panel _detailPanel;
        private ComboBox _gameVer, _loaderVer;
        private List<string> _loaderVersions;

        private static readonly (string Name, string Glyph, string Desc)[] LoaderInfo =
        {
            ("Fabric", "\uE90F", "轻量、更新快、新版本秒适配。模组生态主流首选，大多数新模组都优先支持。"),
            ("Quilt",  "\uEA86", "Fabric 的社区分支，完全兼容 Fabric 模组，还带自己的独占功能。"),
            ("Forge",  "\uE943", "老牌大哥，历史模组几乎都在这。安装走官方安装器，第一次较慢。"),
        };

        public LoadersPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();
            UpdateLoaderSelection();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_loaderCards == null) return;
            int w = Math.Max(700, Width - 64);
            int gap = 17;
            int cw = (w - gap * 2) / 3;
            for (int i = 0; i < 3; i++)
                _loaderCards[i].Bounds = new Rectangle(32 + i * (cw + gap), 90, cw, 170);
            _detailPanel.Bounds = new Rectangle(32, 280, w, Math.Max(220, Height - 310));
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("加载器"));

            // ===== 三张加载器卡 =====
            _loaderCards = new W11Card[3];
            int x = 32;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var card = new W11Card { Bounds = new Rectangle(x, 90, 314, 170), Tag = i };
                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Glyph,
                    Font = Theme.BigIconFont,
                    ForeColor = Theme.Accent,
                    BackColor = Color.Transparent,
                    Location = new Point(24, 18),
                    AutoSize = true
                });
                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Name,
                    Font = new Font(Theme.DefaultFontFamily, 14f, FontStyle.Bold),
                    BackColor = Color.Transparent,
                    ForeColor = Theme.Foreground,
                    Location = new Point(80, 24),
                    AutoSize = true
                });
                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Desc,
                    Font = Theme.SmallFont,
                    BackColor = Color.Transparent,
                    ForeColor = Theme.ForegroundDim,
                    Bounds = new Rectangle(24, 70, 268, 60)
                });
                var chooseBtn = new W11Button
                {
                    Text = "选择 " + LoaderInfo[i].Name,
                    ButtonKind = W11Button.Kind.Standard,
                    Bounds = new Rectangle(24, 128, 160, 30),
                    Font = Theme.SmallFont
                };
                chooseBtn.Click += (s, e) => { _selectedLoader = idx; UpdateLoaderSelection(); };
                card.Controls.Add(chooseBtn);

                _loaderCards[i] = card;
                Controls.Add(card);
                x += 331;
            }

            // ===== 安装面板 =====
            _detailPanel = new Panel { Bounds = new Rectangle(32, 280, 976, 300), BackColor = Color.Transparent };

            var detailCard = new W11Card { Dock = DockStyle.Fill };
            _detailPanel.Controls.Add(detailCard);
            Controls.Add(_detailPanel);

            int y = 24;
            detailCard.Controls.Add(new Label
            {
                Text = "① 选择游戏版本（需要先安装原版）",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(28, y),
                AutoSize = true
            });
            y += 28;
            _gameVer = new ComboBox
            {
                Bounds = new Rectangle(28, y, 420, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var v in GameInstaller.ListInstalledVersions(_main.Cfg.GameDirectory))
                _gameVer.Items.Add(v);
            if (_gameVer.Items.Count > 0) _gameVer.SelectedIndex = 0;
            detailCard.Controls.Add(_gameVer);
            y += 56;

            detailCard.Controls.Add(new Label
            {
                Text = "② 获取加载器版本列表",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(28, y),
                AutoSize = true
            });
            y += 28;

            var fetchBtn = new W11Button
            {
                Text = "获取版本列表",
                IconGlyph = "\uE896",
                Bounds = new Rectangle(28, y, 170, 36)
            };
            fetchBtn.Click += (s, e) => _ = FetchLoaderVersions();
            detailCard.Controls.Add(fetchBtn);

            _loaderVer = new ComboBox
            {
                Bounds = new Rectangle(214, y, 420, 36),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            detailCard.Controls.Add(_loaderVer);
            y += 56;

            var installBtn = new W11Button
            {
                Text = "安装加载器",
                IconGlyph = "\uE73E",  // CheckMark
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(28, y, 220, 48)
            };
            installBtn.Click += (s, e) => Install();
            detailCard.Controls.Add(installBtn);

            detailCard.Controls.Add(new Label
            {
                Text = "提示：Fabric / Quilt 由启动器直接安装，全自动；Forge 会下载官方安装器并用本机 Java 执行。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(270, y + 14),
                AutoSize = true
            });
        }

        private void UpdateLoaderSelection()
        {
            for (int i = 0; i < 3; i++)
            {
                bool sel = i == _selectedLoader;
                _loaderCards[i].Invalidate();
                // 选中卡片：把按钮换成强调色
                foreach (Control c in _loaderCards[i].Controls)
                    if (c is W11Button b && b.Text.StartsWith("选择"))
                        b.ButtonKind = sel ? W11Button.Kind.Accent : W11Button.Kind.Standard;
            }
        }

        private async Task FetchLoaderVersions()
        {
            if (_gameVer.SelectedItem == null)
            {
                MessageBox.Show("先去「下载」页装个原版游戏再装加载器！", "Easyer");
                _main.Navigate("download");
                return;
            }
            var game = _gameVer.SelectedItem.ToString();
            _main.SetStatus("获取加载器版本 ...");
            try
            {
                _loaderVersions = await Task.Run(() =>
                {
                    return _selectedLoader switch
                    {
                        0 => FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Fabric, game),
                        1 => FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Quilt, game),
                        _ => ForgeInstaller.GetVersions(game).Select(f => f.Build).ToList()
                    };
                });
                _loaderVer.Items.Clear();
                foreach (var v in _loaderVersions) _loaderVer.Items.Add(v);
                if (_loaderVer.Items.Count > 0) _loaderVer.SelectedIndex = 0;
                _main.SetStatus("加载器版本已获取，共 " + _loaderVersions.Count + " 个");
            }
            catch (Exception ex)
            {
                _main.SetStatus("获取失败：" + ex.Message);
            }
        }

        private void Install()
        {
            if (_gameVer.SelectedItem == null || _loaderVer.SelectedItem == null)
            {
                MessageBox.Show("游戏版本和加载器版本都得选！", "Easyer");
                return;
            }
            var game = _gameVer.SelectedItem.ToString();
            var loader = _loaderVer.SelectedItem.ToString();

            _main.SetStatus("开始安装加载器 ...");
            Task.Run(() =>
            {
                try
                {
                    if (_selectedLoader == 2)
                    {
                        ForgeInstaller.Install(game, loader, _main.Cfg.GameDirectory, _main.Cfg.JavaPath,
                            msg => _main.SetStatus("[Forge] " + msg));
                    }
                    else
                    {
                        var kind = _selectedLoader == 0 ? FabricInstaller.LoaderKind.Fabric : FabricInstaller.LoaderKind.Quilt;
                        FabricInstaller.Install(kind, game, loader, _main.Cfg.GameDirectory,
                            msg => _main.SetStatus("[加载器] " + msg));
                    }
                    _main.SetStatus("加载器安装完成 ✓");
                    MessageBox.Show("安装完成！主页版本列表里会出现对应版本。", "Easyer",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _main.SetStatus("加载器安装失败：" + ex.Message);
                    MessageBox.Show("安装失败：\n" + ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }
    }
}
