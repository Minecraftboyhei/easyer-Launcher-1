using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Marketplace;
using EasyerLauncher.Core.Minecraft;

namespace EasyerLauncher.Pages
{
    /// <summary>MOD / 资源下载：分类胶囊 + 图标内容卡片网格 + 版本选择弹窗。</summary>
    public class ModsPage : Panel
    {
        private readonly MainForm _main;
        private FlowLayoutPanel _flow;
        private TextBox _search;
        private W11Button[] _chips;
        private readonly string[] _chipNames = { "模组", "资源包", "光影", "数据包" };
        private readonly ModrinthClient.ContentType[] _types =
        {
            ModrinthClient.ContentType.Mods,
            ModrinthClient.ContentType.ResourcePack,
            ModrinthClient.ContentType.Shader,
            ModrinthClient.ContentType.DataPack,
        };

        public ModsPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();
            _ = Search("");
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_flow != null)
                _flow.Bounds = new Rectangle(32, 138, Math.Max(600, Width - 64), Math.Max(300, Height - 162));
        }

        private ModrinthClient.ContentType CurrentType => _types[Array.IndexOf(_chips, _chips.First(c => c.Selected))];

        private int _searchGen;   // 搜索代数：丢弃过期结果，防止快速点击时互相覆盖

        private void Build()
        {
            Controls.Add(PageUtil.Title("模组 / 资源"));

            // 分类胶囊
            _chips = new W11Button[_chipNames.Length];
            int cx = 32;
            for (int i = 0; i < _chipNames.Length; i++)
            {
                int idx = i;
                var chip = new W11Button
                {
                    Text = _chipNames[i],
                    ButtonKind = W11Button.Kind.Chip,
                    Bounds = new Rectangle(cx, 88, 92, 34),
                    Font = Theme.SmallFont,
                    Selected = i == 0
                };
                chip.Click += (s, e) =>
                {
                    for (int j = 0; j < _chips.Length; j++)
                    {
                        _chips[j].Selected = j == idx;
                        _chips[j].Invalidate();
                    }
                    _ = Search(_search.Text.Trim());
                };
                _chips[i] = chip;
                Controls.Add(chip);
                cx += 100;
            }

            // 搜索框
            _search = new TextBox
            {
                Bounds = new Rectangle(cx + 12, 90, 300, 31),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "搜索，如：sodium / 全新材质 / 光影",
                Font = Theme.BodyFont
            };
            _search.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = Search(_search.Text.Trim()); };
            Controls.Add(_search);

            var searchBtn = new W11Button
            {
                Text = "搜索",
                IconGlyph = "\uE721",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(cx + 322, 89, 96, 33)
            };
            searchBtn.Click += (s, e) => _ = Search(_search.Text.Trim());
            Controls.Add(searchBtn);

            var hint = new Label
            {
                Text = "",
                Name = "countHint",
                BackColor = Color.Transparent,
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                Location = new Point(cx + 434, 95),
                AutoSize = true
            };
            Controls.Add(hint);

            // 卡片流
            _flow = new FlowLayoutPanel
            {
                Bounds = new Rectangle(32, 138, 976, 542),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            Controls.Add(_flow);
        }

        private string GameVerFilter()
        {
            try
            {
                var list = GameInstaller.ListInstalledVersions(_main.Cfg.GameDirectory);
                return list.LastOrDefault(v => !v.Contains("fabric") && !v.Contains("quilt") && !v.Contains("forge"));
            }
            catch { return null; }
        }

        private async Task Search(string query)
        {
            int gen = ++_searchGen;
            _main.SetStatus("搜索中 ...");
            _flow.Controls.Clear();

            var hint = Controls.Find("countHint", true).FirstOrDefault() as Label;
            if (hint != null) hint.Text = "";

            try
            {
                var t = CurrentType;
                var gameVer = GameVerFilter();
                var results = await Task.Run(() => ModrinthClient.Search(query, t, gameVer, null, 30));
                if (gen != _searchGen || IsDisposed) return;   // 已有更新的搜索，丢弃这批

                if (hint != null)
                    hint.Text = results.Count + " 条结果" + (gameVer != null ? "（按 " + gameVer + " 过滤）" : "");

                _flow.SuspendLayout();
                foreach (var r in results)
                {
                    var card = new ContentCard
                    {
                        CardTitle = r.Title,
                        Sub = "↓ " + ModrinthClient.FormatDownloads(r.Downloads) +
                              (r.ClientSide == "required" ? "  ·  客户端必装" : "") +
                              (r.ClientSide == "unsupported" ? "  ·  仅服务端" : ""),
                        Desc = string.IsNullOrEmpty(r.Description) ? "" : r.Description,
                        IconUrl = r.IconUrl,
                        IconKey = r.Slug,
                        Size = new Size(292, 118),
                        Margin = new Padding(5),
                        OnOpen = () => ShowDetail(r, t),
                        OnAction = () => ShowDetail(r, t)
                    };
                    _flow.Controls.Add(card);
                    card.LoadIcon(null);
                }
                _flow.ResumeLayout(true);
                _main.SetStatus("搜索完成");
            }
            catch (Exception ex)
            {
                if (gen != _searchGen || IsDisposed) return;
                if (hint != null) hint.Text = "";
                _main.SetStatus("搜索失败：" + ex.Message);
                _flow.Controls.Add(new Label
                {
                    Text = "加载失败：" + ex.Message + "\n\n请检查网络后点「搜索」重试。",
                    ForeColor = Theme.ForegroundDim,
                    BackColor = Color.Transparent,
                    Font = Theme.BodyFont,
                    AutoSize = true,
                    Padding = new Padding(16)
                });
            }
        }

        /// <summary>版本文件选择弹窗。</summary>
        private void ShowDetail(ModrinthClient.SearchResult r, ModrinthClient.ContentType t)
        {
            _main.SetStatus("获取 " + r.Title + " 的版本文件 ...");
            Task.Run(() =>
            {
                try
                {
                    var files = ModrinthClient.GetVersions(r.Slug, GameVerFilter(), null);
                    if (files.Count == 0)
                    {
                        _main.SetStatus(r.Title + " 没有适配当前版本的文件");
                        MessageBox.Show("没有找到适配当前游戏版本的文件。\n（可尝试先安装该内容支持的游戏版本）", "Easyer");
                        return;
                    }
                    BeginInvoke(new Action(() =>
                    {
                        using (var dlg = new FilePickForm(_main, r.Title, files, t, _main.Cfg.GameDirectory))
                            dlg.ShowDialog(FindForm());
                    }));
                    _main.SetStatus("已打开 " + r.Title + " 的版本列表");
                }
                catch (Exception ex)
                {
                    _main.SetStatus("获取版本失败：" + ex.Message);
                }
            });
        }
    }

    /// <summary>版本文件选择弹窗（深色跟随主题）。</summary>
    internal class FilePickForm : Form
    {
        private readonly List<ModrinthClient.VersionFile> _files;
        private readonly ModrinthClient.ContentType _type;
        private readonly string _gameDir;
        private readonly MainForm _main;
        private readonly ListBox _list;

        public FilePickForm(MainForm main, string title, List<ModrinthClient.VersionFile> files,
                            ModrinthClient.ContentType type, string gameDir)
        {
            _main = main;
            _files = files;
            _type = type;
            _gameDir = gameDir;

            Text = title + " — 选择版本";
            Size = new Size(640, 520);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            _list = new ListBox
            {
                Bounds = new Rectangle(20, 20, 584, 370),
                BorderStyle = BorderStyle.None,
                Font = Theme.SmallFont,
                IntegralHeight = false
            };
            foreach (var f in files)
                _list.Items.Add(string.Format("{0}   ({1})   [{2}]   {3}",
                    f.VersionNumber, ModrinthClient.FormatSize(f.Size),
                    string.Join("/", f.Loaders.Count > 0 ? f.Loaders : new List<string> { "any" }),
                    f.GameVersions.FirstOrDefault() ?? ""));
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            Controls.Add(_list);

            var dlBtn = new W11Button
            {
                Text = "下载所选版本",
                IconGlyph = "\uE896",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(20, 410, 220, 44),
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold)
            };
            dlBtn.Click += (s, e) => { Download(); Close(); };
            Controls.Add(dlBtn);

            var cancel = new W11Button
            {
                Text = "取消",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(256, 410, 120, 44)
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            Theme.ApplyTo(this);
            Theme.EnableWin11Window(this);
        }

        private void Download()
        {
            if (_list.SelectedIndex < 0) return;
            var file = _files[_list.SelectedIndex];
            _main.SetStatus("开始下载 " + file.FileName + " ...");
            Task.Run(() =>
            {
                try
                {
                    ModrinthClient.Download(file, _type, _gameDir, msg => _main.SetStatus("[内容] " + msg));
                    MessageBox.Show("下载完成！已放入游戏目录对应文件夹。", "Easyer",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _main.SetStatus("下载失败：" + ex.Message);
                    MessageBox.Show("下载失败：\n" + ex.Message, "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }
    }
}
