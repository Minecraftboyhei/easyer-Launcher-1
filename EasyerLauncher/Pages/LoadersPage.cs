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
        private W11Button[] _chooseBtns;
        private Panel _detailPanel;
        private W11Card _detailCard;
        private ChipPicker _gameVer;
        private CardPicker _verPick;
        private W11Button _fetchBtn, _installBtn;
        private Label _verHint, _tip;
        private List<string> _loaderVersions;

        private static readonly (string Key, string Name, string Desc)[] LoaderInfo =
        {
            ("fabric",   "Fabric",   "轻量、更新快，新版本适配最快，模组生态主流首选。"),
            ("quilt",    "Quilt",    "Fabric 社区分支，兼容 Fabric 模组，另有独占功能。"),
            ("forge",    "Forge",    "老牌加载器，历史模组几乎都在这，首次安装较慢。"),
            ("neoforge", "NeoForge", "Forge 新锐分支，1.20.1+ 专属，新模组增长最快。"),
        };

        public LoadersPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            AutoScroll = true;          // 窗口矮的时候下方安装面板要能滚到
            Build();
            UpdateLoaderSelection();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Relayout();
        }

        private void Relayout()
        {
            if (_loaderCards == null || _detailCard == null) return;
            int w = Math.Max(700, Width - 64);
            int gap = 12;
            int cw = (w - gap * 3) / 4;

            for (int i = 0; i < 4; i++)
            {
                var card = _loaderCards[i];
                card.Bounds = new Rectangle(32 + i * (cw + gap), 90, cw, 196);
                foreach (Control c in card.Controls)
                {
                    if (c is Label l && l.Tag is "desc")
                        l.Bounds = new Rectangle(20, 64, cw - 40, 70);
                    if (c is W11Button b)
                        b.Bounds = new Rectangle(20, 140, Math.Min(140, cw - 40), 34);
                }
            }

            _detailPanel.Bounds = new Rectangle(32, 302, w, Math.Max(280, Height - 332));
            int dw = _detailCard.Width;

            // ② 的提示与按钮按宽度排布，避免长文案被按钮压住
            _verHint.Bounds = new Rectangle(28, 126, Math.Max(200, dw - 200), 20);
            _fetchBtn.Bounds = new Rectangle(Math.Max(28, dw - 158), 120, 130, 32);
            _verPick.Bounds = new Rectangle(24, 150, Math.Max(300, dw - 48), Math.Max(110, _detailCard.Height - 210));
            _installBtn.Location = new Point(24, Math.Max(200, _detailCard.Height - 58));
            _tip.Bounds = new Rectangle(260, _installBtn.Top + 12, Math.Max(200, dw - 284), 32);
            _gameVer.Bounds = new Rectangle(24, 48, Math.Max(300, dw - 48), Math.Max(60, Math.Min(96, _detailCard.Height - 214)));
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("加载器"));

            // ===== 四张加载器卡 =====
            _loaderCards = new W11Card[4];
            _chooseBtns = new W11Button[4];
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new W11Card { Bounds = new Rectangle(32 + i * 250, 90, 238, 196), Tag = i };
                var key = LoaderInfo[i].Key;

                card.Controls.Add(new KindBadge { Kind = key, Location = new Point(20, 18), Size = new Size(34, 34) });

                card.Controls.Add(new Label
                {
                    Text = LoaderInfo[i].Name,
                    Font = new Font(Theme.DefaultFontFamily, 12.5f, FontStyle.Bold),
                    BackColor = Color.Transparent,
                    ForeColor = Theme.Foreground,
                    Location = new Point(64, 24),
                    AutoSize = true
                });

                var desc = new Label
                {
                    Text = LoaderInfo[i].Desc,
                    Font = Theme.SmallFont,
                    BackColor = Color.Transparent,
                    ForeColor = Theme.ForegroundDim,
                    Tag = "desc",
                    Bounds = new Rectangle(20, 64, 198, 70)
                };
                card.Controls.Add(desc);

                var chooseBtn = new W11Button
                {
                    Text = "选择",
                    ButtonKind = W11Button.Kind.Standard,
                    Bounds = new Rectangle(20, 140, 130, 34),
                    Font = Theme.SmallFont
                };
                chooseBtn.Click += (s, e) => { _selectedLoader = idx; UpdateLoaderSelection(); };
                _chooseBtns[i] = chooseBtn;
                card.Controls.Add(chooseBtn);

                _loaderCards[i] = card;
                Controls.Add(card);
            }

            // ===== 安装面板 =====
            _detailPanel = new Panel { Bounds = new Rectangle(32, 302, 976, 320), BackColor = Color.Transparent };
            _detailCard = new W11Card { Dock = DockStyle.Fill };
            _detailPanel.Controls.Add(_detailCard);
            Controls.Add(_detailPanel);

            _detailCard.Controls.Add(Hint("① 选择游戏版本（需要先装好原版）", 28, 22));
            _gameVer = new ChipPicker { Bounds = new Rectangle(24, 48, 720, 72) };
            ReloadGameVersions();
            _detailCard.Controls.Add(_gameVer);

            _verHint = Hint("② 点「获取版本」拉取可用的加载器版本", 28, 126);
            _detailCard.Controls.Add(_verHint);

            _fetchBtn = new W11Button
            {
                Text = "获取版本",
                IconGlyph = "download",
                ButtonKind = W11Button.Kind.Standard,
                Bounds = new Rectangle(660, 120, 130, 32),
                Font = Theme.SmallFont
            };
            _fetchBtn.Click += (s, e) => _ = FetchLoaderVersions();
            _detailCard.Controls.Add(_fetchBtn);

            _verPick = new CardPicker { Bounds = new Rectangle(24, 150, 860, 118), CardSize = new Size(196, 58) };
            _detailCard.Controls.Add(_verPick);

            _installBtn = new W11Button
            {
                Text = "安装加载器",
                IconGlyph = "install",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(24, 284, 220, 44)
            };
            _installBtn.Click += (s, e) => Install();
            _detailCard.Controls.Add(_installBtn);

            _tip = new Label
            {
                Text = "Fabric / Quilt 由启动器直接安装，全自动；Forge / NeoForge 会下载官方安装器并用本机 Java 执行，第一次较慢。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(264, 296, 480, 32)
            };
            _detailCard.Controls.Add(_tip);

            Relayout();
        }

        private static Label Hint(string text, int x, int y) => new Label
        {
            Text = text,
            ForeColor = Theme.ForegroundDim,
            BackColor = Color.Transparent,
            Font = Theme.SmallFont,
            Location = new Point(x, y),
            AutoSize = true
        };

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
                _chooseBtns[i].Text = sel ? "已选择" : "选择";
                _chooseBtns[i].ButtonKind = sel ? W11Button.Kind.Accent : W11Button.Kind.Standard;
                _chooseBtns[i].Invalidate();
                _loaderCards[i].Invalidate();
            }
            if (_verPick != null)
            {
                _verPick.Clear();
                _verHint.Text = "② 点「获取版本」拉取可用的 " + LoaderInfo[_selectedLoader].Name + " 版本";
            }
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
            var info = LoaderInfo[_selectedLoader];
            _main.SetStatus("获取 " + info.Name + " 版本 ...");
            _fetchBtn.Enabled = false;
            _main.BeginTask("获取 " + info.Name + " 版本");
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

                _verPick.SetItems(_loaderVersions.Select((v, i) => new CardPicker.Item
                {
                    Title = v,
                    Sub = info.Name + " 加载器",
                    Badge = i == 0 ? "最新" : "",
                    IconKind = info.Key
                }));

                _main.SetStatus(_loaderVersions.Count > 0
                    ? info.Name + " 版本已获取，共 " + _loaderVersions.Count + " 个"
                    : "该游戏版本没有可用的 " + info.Name + " 版本");
            }
            catch (Exception ex)
            {
                _main.SetStatus("获取失败：" + ex.Message);
            }
            finally
            {
                _fetchBtn.Enabled = true;
                _main.EndTask();
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
            var info = LoaderInfo[_selectedLoader];
            int idx = _selectedLoader;

            _main.SetStatus("开始安装 " + info.Name + " ...");
            _main.BeginTask("安装 " + info.Name + " " + loader);
            Task.Run(() =>
            {
                try
                {
                    switch (idx)
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
                            var kind = idx == 0 ? FabricInstaller.LoaderKind.Fabric : FabricInstaller.LoaderKind.Quilt;
                            FabricInstaller.Install(kind, game, loader, _main.Cfg.GameDirectory,
                                msg => _main.SetStatus("[加载器] " + msg));
                            break;
                    }
                    _main.SetStatus(info.Name + " 安装完成 ✓");
                    _main.RefreshHomeVersions();
                    BeginInvoke(new Action(() =>
                    {
                        ReloadGameVersions();
                        _main.EndTask();
                        MessageBox.Show("安装完成！主页版本列表里会出现对应版本。", "Easyer",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (Exception ex)
                {
                    _main.SetStatus("加载器安装失败：" + ex.Message);
                    BeginInvoke(new Action(() =>
                    {
                        _main.EndTask();
                        MessageBox.Show("安装失败：\n" + ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }
    }
}
