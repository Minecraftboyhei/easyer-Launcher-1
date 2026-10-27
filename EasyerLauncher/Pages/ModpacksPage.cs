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
    /// <summary>整合包：搜索 + 图标卡片网格 + 图形化的版本选择与安装。</summary>
    public class ModpacksPage : Panel
    {
        private readonly MainForm _main;
        private FlowLayoutPanel _flow;
        private TextBox _search;
        private Label _hint;
        private int _searchGen;

        public ModpacksPage(MainForm main)
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

        private void Build()
        {
            Controls.Add(PageUtil.Title("整合包"));

            _search = new TextBox
            {
                Bounds = new Rectangle(32, 90, 380, 31),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "搜索整合包，如：cobblemon / 机械动力 / 生存",
                Font = Theme.BodyFont
            };
            _search.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = Search(_search.Text.Trim()); };
            Controls.Add(_search);

            var btn = new W11Button
            {
                Text = "搜索",
                IconGlyph = "\uE721",
                ButtonKind = W11Button.Kind.Accent,
                Bounds = new Rectangle(424, 89, 96, 33)
            };
            btn.Click += (s, e) => _ = Search(_search.Text.Trim());
            Controls.Add(btn);

            var hot = new W11Button
            {
                Text = "热门整合包",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(532, 89, 116, 33),
                Font = Theme.SmallFont
            };
            hot.Click += (s, e) => { _search.Text = ""; _ = Search(""); };
            Controls.Add(hot);

            _hint = new Label
            {
                Text = "",
                BackColor = Color.Transparent,
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                Location = new Point(664, 95),
                AutoSize = true
            };
            Controls.Add(_hint);

            _flow = new FlowLayoutPanel
            {
                Bounds = new Rectangle(32, 138, 976, 542),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            Controls.Add(_flow);
        }

        private async Task Search(string query)
        {
            int gen = ++_searchGen;
            _main.SetStatus("搜索整合包中 ...");
            _flow.Controls.Clear();
            _hint.Text = "";

            try
            {
                var results = await Task.Run(() =>
                    ModrinthClient.Search(query, ModrinthClient.ContentType.Modpack, null, null, 30));
                if (gen != _searchGen || IsDisposed) return;

                _hint.Text = results.Count + " 个整合包";
                _flow.SuspendLayout();
                foreach (var r in results)
                {
                    var card = new ContentCard
                    {
                        CardTitle = r.Title,
                        Sub = "↓ " + ModrinthClient.FormatDownloads(r.Downloads) +
                              (r.Versions.Count > 0 ? "  ·  " + r.Versions.LastOrDefault() : ""),
                        Desc = r.Description,
                        IconUrl = r.IconUrl,
                        IconKey = r.Slug,
                        Size = new Size(292, 118),
                        Margin = new Padding(5),
                        OnOpen = () => PickVersion(r),
                        OnAction = () => PickVersion(r)
                    };
                    _flow.Controls.Add(card);
                    card.LoadIcon(null);
                }
                _flow.ResumeLayout(true);
                _main.SetStatus("整合包搜索完成");
            }
            catch (Exception ex)
            {
                if (gen != _searchGen || IsDisposed) return;
                _main.SetStatus("搜索失败：" + ex.Message);
            }
        }

        private void PickVersion(ModrinthClient.SearchResult r)
        {
            _main.SetStatus("获取 " + r.Title + " 的版本 ...");
            Task.Run(() =>
            {
                try
                {
                    var files = ModrinthClient.GetVersions(r.Slug, null, null);
                    if (files.Count == 0)
                    {
                        BeginInvoke(new Action(() => MessageBox.Show("这个整合包没有可下载的版本。", "Easyer")));
                        return;
                    }
                    BeginInvoke(new Action(() =>
                    {
                        using (var dlg = new PackPickForm(_main, r, files))
                            dlg.ShowDialog(FindForm());
                    }));
                }
                catch (Exception ex)
                {
                    _main.SetStatus("获取版本失败：" + ex.Message);
                }
            });
        }
    }

    /// <summary>整合包版本选择弹窗：图形卡片，选定即装。</summary>
    internal class PackPickForm : Form
    {
        private readonly MainForm _main;
        private readonly ModrinthClient.SearchResult _project;
        private readonly List<ModrinthClient.VersionFile> _files;
        private readonly CardPicker _picker;
        private readonly Label _info;

        public PackPickForm(MainForm main, ModrinthClient.SearchResult project, List<ModrinthClient.VersionFile> files)
        {
            _main = main;
            _project = project;
            _files = files;

            Text = project.Title + " — 选择版本";
            Size = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            Controls.Add(new Label
            {
                Text = project.Title,
                Font = new Font(Theme.DefaultFontFamily, 15f, FontStyle.Bold),
                ForeColor = Theme.Foreground,
                BackColor = Color.Transparent,
                Location = new Point(24, 20),
                AutoSize = true
            });

            Controls.Add(new Label
            {
                Text = project.Description,
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(24, 54, 700, 40)
            });

            _picker = new CardPicker
            {
                Bounds = new Rectangle(24, 104, 704, 330),
                CardSize = new Size(220, 62)
            };
            _picker.SetItems(files.Select(f => new CardPicker.Item
            {
                Title = f.VersionNumber,
                Sub = (f.GameVersions.FirstOrDefault() ?? "?") +
                      (f.Loaders.Count > 0 ? "  ·  " + f.Loaders[0] : "") +
                      "  ·  " + ModrinthClient.FormatSize(f.Size)
            }));
            _picker.SelectionChanged += () => UpdateInfo();
            Controls.Add(_picker);

            _info = new Label
            {
                Text = "",
                Font = Theme.SmallFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(24, 442, 704, 22)
            };
            Controls.Add(_info);
            UpdateInfo();

            var install = new W11Button
            {
                Text = "安装整合包",
                IconGlyph = "\uE896",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(24, 468, 200, 44)
            };
            install.Click += (s, e) => { Install(); Close(); };
            Controls.Add(install);

            var cancel = new W11Button
            {
                Text = "取消",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(240, 468, 120, 44)
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            Theme.ApplyTo(this);
            Theme.EnableWin11Window(this);
        }

        private ModrinthClient.VersionFile Current =>
            _picker.SelectedIndex >= 0 && _picker.SelectedIndex < _files.Count ? _files[_picker.SelectedIndex] : null;

        private void UpdateInfo()
        {
            var f = Current;
            _info.Text = f == null ? "" :
                "将安装到独立实例目录（游戏版本与加载器自动配置，mods/存档与其他版本互不干扰）";
        }

        private void Install()
        {
            var f = Current;
            if (f == null) return;

            _main.SetStatus("开始安装整合包 " + _project.Title + " ...");
            Task.Run(() =>
            {
                try
                {
                    var instId = MrpackInstaller.Install(f.Url, _project.Slug, _main.Cfg.GameDirectory,
                        _main.Cfg.JavaPath, msg => _main.SetStatus("[整合包] " + msg));

                    var instDir = System.IO.Path.Combine(_main.Cfg.GameDirectory, "instances", instId);
                    var existing = _main.Cfg.Instances.Find(i =>
                        string.Equals(i.Id, instId, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        _main.Cfg.Instances.Add(new Core.Config.InstanceInfo
                        {
                            Id = instId,
                            Name = _project.Title,
                            GameVersion = f.GameVersions.FirstOrDefault() ?? "",
                            Loader = f.Loaders.FirstOrDefault() ?? "",
                            IconUrl = _project.IconUrl,
                            PackSlug = _project.Slug,
                            Dir = instDir
                        });
                    }
                    _main.Cfg.Save();
                    _main.RefreshHomeVersions();

                    _main.SetStatus("整合包安装完成：" + _project.Title + " ✓");
                    BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(
                            "整合包《" + _project.Title + "》安装完成！\n\n" +
                            "它已作为独立实例安装，主页版本下拉里选它即可启动，\n" +
                            "mods、存档、配置都和其他版本隔离。",
                            "Easyer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _main.Navigate("home");
                    }));
                }
                catch (Exception ex)
                {
                    _main.SetStatus("整合包安装失败：" + ex.Message);
                    BeginInvoke(new Action(() =>
                        MessageBox.Show("安装失败：\n" + ex.Message, "Easyer",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            });
        }
    }
}
