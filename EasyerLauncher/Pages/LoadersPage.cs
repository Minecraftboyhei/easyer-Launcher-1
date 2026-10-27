using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;

namespace EasyerLauncher.Pages
{
    /// <summary>加载器：四张加载器卡片 + 选中后展开安装面板（游戏版本与加载器版本都用图形卡片选）。</summary>
    public class LoadersPage : Panel
    {
        private readonly MainForm _main;
        private int _selectedLoader = 0;   // 0 Fabric 1 Quilt 2 Forge 3 NeoForge
        private W11Card[] _loaderCards;
        private Panel _detailPanel;
        private ChipPicker _gameVer;
        private CardPicker _verPick;
        private List<string> _loaderVersions;
        private W11Card _detailCard;

        private static readonly (string Name, string Glyph, string Desc)[] LoaderInfo =
        {
            ("Fabric",   "\uE90F", "轻量、更新快、新版本秒适配。模组生态主流首选，大多数新模组都优先支持。"),
            ("Quilt",    "\uEA86", "Fabric 的社区分支，完全兼容 Fabric 模组，还带自己的独占功能。"),
            ("Forge",    "\uE943", "老牌大哥，历史模组几乎都在这。安装走官方安装器，第一次较慢。"),
            ("NeoForge", "\uE720", "Forge 的新锐分支，1.20.1+ 专属，模组生态增长最快的新家。"),
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
            int gap = 12;
            int cw = (w - gap * 3) / 4;
            for (int i = 0; i < 4; i++)
                _loaderCards[i].Bounds = new Rectangle(32 + i * (cw + gap), 90, cw, 170);
            _detailPanel.Bounds = new Rectangle(32, 280, w, Math.Max(260, Height - 310));
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("加载器"));

            // ===== 四张加载器卡 =====
            _loaderCards = new W11Card[4];
            int x = 32;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new W11Card { Bounds = new Rectangle(x, 90, 238, 170), Tag = i };
                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Glyph,
                    Font = Theme.BigIconFont,
                    ForeColor = Theme.Accent,
                    BackColor = Color.Transparent,
                    Location = new Point(20, 16),
                    AutoSize = true
                });
                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Name,
                    Font = new Font(Theme.DefaultFontFamily, 12.5f, FontStyle.Bold),
                    BackColor = Color.Transparent,
                    ForeColor = Theme.Foreground,
                    Location = new Point(64, 22),
                    AutoSize = true
                });
                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Desc,
                    Font = Theme.SmallFont,
                    BackColor = Color.Transparent,
                    ForeColor = Theme.ForegroundDim,
                    Bounds = new Rectangle(20, 64, 198, 64)
                });
                var chooseBtn = new W11Button
                {
                    Text = "选择",
                    ButtonKind = W11Button.Kind.Standard,
                    Bounds = new Rectangle(20, 128, 130, 30),
                    Font = Theme.SmallFont
                };
                chooseBtn.Click += (s, e) => { _selectedLoader = idx; UpdateLoaderSelection(); };
                card.Controls.Add(chooseBtn);

                _loaderCards[i] = card;
                Controls.Add(card);
                x += 250;
            }

            // ===== 安装面板 =====
            _detailPanel = new Panel { Bounds = new Rectangle(32, 280, 976, 320), BackColor = Color.Transparent };
            _detailCard = new W11Card { Dock = DockStyle.Fill };
            _detailPanel.Controls.Add(_detailCard);
            Controls.Add(_detailPanel);

            // ① 游戏版本（图形化胶囊）
            _detailCard.Controls.Add(new Label
            {
                Text = "① 选择游戏版本（需要先安装原版）",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(28, 22),
                AutoSize = true
            });
            _gameVer = new ChipPicker { Bounds = new Rectangle(24, 48, 720, 72) };
            ReloadGameVersions();
            _detailCard.Controls.Add(_gameVer);

            // ② 加载器版本（图形化卡片）
            _detailCard.Controls.Add(new Label
            {
                Text = "② 选择加载器版本（点右侧「获取版本」加载）",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(28, 126),
                AutoSize = true
            });

            var fetchBtn = new W11Button
            {
                Text = "获取版本",
                IconGlyph = "\uE896",
                ButtonKind = W11Button.Kind.Standard,
                Bounds = new Rectangle(660, 120, 130, 32),
                Font = Theme.SmallFont
            };
            fetchBtn.Click += (s, e) => _ = FetchLoaderVersions();
            _detailCard.Controls.Add(fetchBtn);

            _verPick = new CardPicker { Bounds = new Rectangle(24, 158, 860, 118), CardSize = new Size(196, 58) };
            _detailCard.Controls.Add(_verPick);

            var installBtn = new W11Button
            {
                Text = "安装加载器",
                IconGlyph = "\uE73E",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(24, 284, 220, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            };
            installBtn.Click += (s, e) => Install();
            _detailCard.Controls.Add(installBtn);

            var tip = new Label
            {
                Text = "Fabric / Quilt 由启动器直接安装，全自动；Forge / NeoForge 会下载官方安装器并用本机 Java 执行，第一次较慢。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(264, 296),
                AutoSize = true
            };
            _detailCard.Controls.Add(tip);
        }

        /// <summary>重新读取已安装版本（切页/装完游戏后刷新）。</summary>
        public void ReloadGameVersions()
        {
            if (_gameVer == null) return;
            var list = GameInstaller.ListInstalledVersions(_main.Cfg.GameDirectory);
            // 整合包实例不作为加载器基底
            list = list.Where(v => !v.StartsWith("pack-", StringComparison.OrdinalIgnoreCase)).ToList();
            _gameVer.SetItems(list);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) ReloadGameVersions();
        }

        private void UpdateLoaderSelection()
        {
            for (int i = 0; i < 4; i++)
            {
                bool sel = i == _selectedLoader;
                _loaderCards[i].Invalidate();
                foreach (Control c in _loaderCards[i].Controls)
                    if (c is W11Button b && b.Text == "选择")
                        b.ButtonKind = sel ? W11Button.Kind.Accent : W11Button.Kind.Standard;
            }
            _verPick?.Clear();
        }

        private async Task FetchLoaderVersions()
        {
            var game = _gameVer?.SelectedItem;
            if (string.IsNullOrEmpty(game))
            {
                MessageBox.Show("先去「下载」页装个原版游戏再装加载器！", "Easyer");
                _main.Navigate("download");
                return;
            }
            _main.SetStatus("获取加载器版本 ...");
            try
            {
                _loaderVersions = await Task.Run(() =>
                {
                    return _selectedLoader switch
                    {
                        0 => FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Fabric, game),
                        1 => FabricInstaller.GetLoaderVersions(FabricInstaller.LoaderKind.Quilt, game),
                        2 => ForgeInstaller.GetVersions(game).Select(f => f.Build).ToList(),
                        _ => NeoForgeInstaller.GetVersions(game)
                    };
                });

                var name = _selectedLoader switch { 0 => "fabric-loader", 1 => "quilt-loader", 2 => "forge", _ => "neoforge" };
                _verPick.SetItems(_loaderVersions.Select((v, i) => new CardPicker.Item
                {
                    Title = v,
                    Sub = name,
                    Badge = i == 0 ? "最新" : ""
                }));

                if (_loaderVersions.Count > 0)
                    _main.SetStatus("加载器版本已获取，共 " + _loaderVersions.Count + " 个");
                else
                    _main.SetStatus("该游戏版本没有可用的 " + LoaderInfo[_selectedLoader].Name + " 版本");
            }
            catch (Exception ex)
            {
                _main.SetStatus("获取失败：" + ex.Message);
            }
        }

        private void Install()
        {
            var loader = _verPick?.SelectedItem?.Title;
            var game = _gameVer?.SelectedItem;
            if (string.IsNullOrEmpty(game) || string.IsNullOrEmpty(loader))
            {
                MessageBox.Show("游戏版本和加载器版本都得选！（先点「获取版本」）", "Easyer");
                return;
            }
            var name = LoaderInfo[_selectedLoader].Name;

            _main.SetStatus("开始安装 " + name + " ...");
            Task.Run(() =>
            {
                try
                {
                    switch (_selectedLoader)
                    {
                        case 2:
                            ForgeInstaller.Install(game, loader, _main.Cfg.GameDirectory, _main.Cfg.JavaPath,
                                msg => _main.SetStatus("[Forge] " + msg));
                            break;
                        case 3:
                            NeoForgeInstaller.Install(game, loader, _main.Cfg.GameDirectory, _main.Cfg.JavaPath,
                                msg => _main.SetStatus("[NeoForge] " + msg));
                            break;
                        default:
                            var kind = _selectedLoader == 0 ? FabricInstaller.LoaderKind.Fabric : FabricInstaller.LoaderKind.Quilt;
                            FabricInstaller.Install(kind, game, loader, _main.Cfg.GameDirectory,
                                msg => _main.SetStatus("[加载器] " + msg));
                            break;
                    }
                    _main.SetStatus(name + " 安装完成 ✓");
                    _main.RefreshHomeVersions();
                    BeginInvoke(new Action(() =>
                    {
                        ReloadGameVersions();
                        MessageBox.Show("安装完成！主页版本列表里会出现对应版本。", "Easyer",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    _main.SetStatus("加载器安装失败：" + ex.Message);
                    BeginInvoke(new Action(() =>
                        MessageBox.Show("安装失败：\n" + ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }
    }
}
