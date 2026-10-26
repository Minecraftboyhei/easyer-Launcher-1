using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;

namespace EasyerLauncher.Pages
{
    /// <summary>加载器：四张图形化加载器卡片 + 选中后展开安装面板（版本列表式选择）。</summary>
    public class LoadersPage : Panel
    {
        private readonly MainForm _main;
        private int _selectedLoader = 0;   // 0 Fabric 1 Quilt 2 Forge 3 NeoForge
        private W11Card[] _loaderCards;
        private Panel _detailPanel;
        private ComboBox _gameVer;
        private LoaderVersionList _verList;
        private List<string> _loaderVersions;

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
            _detailPanel.Bounds = new Rectangle(32, 280, w, Math.Max(240, Height - 310));
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
            y += 30;
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
            y += 52;

            detailCard.Controls.Add(new Label
            {
                Text = "② 获取加载器版本列表（点下方按钮，版本会列在右侧）",
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(28, y),
                AutoSize = true
            });
            y += 30;

            var fetchBtn = new W11Button
            {
                Text = "获取版本列表",
                IconGlyph = "\uE896",
                Bounds = new Rectangle(28, y, 170, 36)
            };
            fetchBtn.Click += (s, e) => _ = FetchLoaderVersions();
            detailCard.Controls.Add(fetchBtn);

            // 版本列表（PCL 式：可滚动列表行，单击选中）
            _verList = new LoaderVersionList
            {
                Bounds = new Rectangle(214, y - 4, 440, 130)
            };
            detailCard.Controls.Add(_verList);
            y += 78;

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
                Text = "提示：Fabric / Quilt 由启动器直接安装，全自动；Forge / NeoForge 会下载官方安装器并用本机 Java 执行，第一次较慢。",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Location = new Point(270, y + 14),
                AutoSize = true
            });
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
            _verList?.ClearItems();
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
                        2 => ForgeInstaller.GetVersions(game).Select(f => f.Build).ToList(),
                        _ => NeoForgeInstaller.GetVersions(game)
                    };
                });
                _verList.SetItems(_loaderVersions, _selectedLoader);
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
            var loader = _verList.SelectedItem;
            if (_gameVer.SelectedItem == null || loader == null)
            {
                MessageBox.Show("游戏版本和加载器版本都得选！（版本列表在右边，先点「获取版本列表」）", "Easyer");
                return;
            }
            var game = _gameVer.SelectedItem.ToString();
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

    /// <summary>PCL 式加载器版本列表（自绘行，单击选中，带强调色高亮）。</summary>
    internal class LoaderVersionList : Control
    {
        private List<string> _items = new();
        private int _selectedIndex = -1;
        private int _loaderIdx = 0;
        private int _scroll;
        private const int RowH = 30;

        public event Action SelectionChanged;
        public Action ItemActivated;  // 双击行 = 直接确认

        public string SelectedItem =>
            _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

        public LoaderVersionList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Input;
        }

        public void ClearItems()
        {
            _items = new List<string>();
            _selectedIndex = -1;
            _scroll = 0;
            Invalidate();
        }

        public void SetItems(List<string> items, int loaderIdx)
        {
            _items = items ?? new List<string>();
            _loaderIdx = loaderIdx;
            _selectedIndex = _items.Count > 0 ? 0 : -1;
            _scroll = 0;
            Invalidate();
            SelectionChanged?.Invoke();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scroll = Math.Max(0, Math.Min(Math.Max(0, _items.Count * RowH - Height), _scroll - Math.Sign(e.Delta) * RowH * 3));
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int row = (e.Y + _scroll) / RowH;
            if (row >= 0 && row < _items.Count)
            {
                _selectedIndex = row;
                Invalidate();
                SelectionChanged?.Invoke();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int row = (e.Y + _scroll) / RowH;
            if (row >= 0 && row < _items.Count)
            {
                _selectedIndex = row;
                Invalidate();
                ItemActivated?.Invoke();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Input);
            var name = _loaderIdx switch { 0 => "fabric-loader", 1 => "quilt-loader", 2 => "forge", _ => "neoforge" };

            for (int i = _scroll / RowH; i * RowH - _scroll < Height && i < _items.Count; i++)
            {
                var rowRect = new Rectangle(6, i * RowH - _scroll + 2, Width - 22, RowH - 4);
                bool sel = i == _selectedIndex;
                using (var path = Theme.RoundedPath(rowRect, 8))
                {
                    using (var b = new SolidBrush(sel ? Color.FromArgb(60, Theme.Accent) : Color.Transparent))
                        g.FillPath(b, path);
                    if (sel) using (var pen = new Pen(Theme.Accent)) g.DrawPath(pen, path);
                }
                TextRenderer.DrawText(g, name + "  " + _items[i],
                    sel ? new Font(Theme.DefaultFontFamily, 9.5f, FontStyle.Bold) : Theme.BodyFont,
                    new Rectangle(rowRect.X + 12, rowRect.Y, rowRect.Width - 12, rowRect.Height),
                    sel ? Theme.Accent : Theme.Foreground,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (_items.Count == 0)
                TextRenderer.DrawText(g, "（点「获取版本列表」加载）", Theme.SmallFont,
                    new Rectangle(0, 0, Width, Height), Theme.ForegroundDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
