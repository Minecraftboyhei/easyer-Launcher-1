using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyerLauncher.Core.Minecraft;

namespace EasyerLauncher.Pages
{
    /// <summary>版本下载：筛选胶囊 + 图形化版本卡片网格。</summary>
    public class DownloadsPage : Panel
    {
        private readonly MainForm _main;
        private FlowLayoutPanel _flow;
        private RoundField _search;
        private W11Button[] _chips;
        private List<VersionManifest.VersionEntry> _versions;
        private readonly string[] _chipNames = { "全部", "正式版", "快照", "Beta", "Alpha" };

        public DownloadsPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();
            _ = LoadAsync();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_flow != null)
                _flow.Bounds = new Rectangle(32, 134, Math.Max(600, Width - 64), Math.Max(300, Height - 158));
            if (_loading != null)
                _loading.Bounds = new Rectangle(32, 134, Math.Max(600, Width - 64), Math.Max(300, Height - 158));
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("下载"));

            // 筛选胶囊
            _chips = new W11Button[_chipNames.Length];
            int cx = 32;
            for (int i = 0; i < _chipNames.Length; i++)
            {
                int idx = i;
                var chip = new W11Button
                {
                    Text = _chipNames[i],
                    ButtonKind = W11Button.Kind.Chip,
                    Bounds = new Rectangle(cx, 88, 82, 32),
                    Font = Theme.SmallFont,
                    Selected = i == 1
                };
                chip.Click += (s, e) =>
                {
                    for (int j = 0; j < _chips.Length; j++)
                    {
                        _chips[j].Selected = j == idx;
                        _chips[j].Invalidate();
                    }
                    ApplyFilter();
                };
                _chips[i] = chip;
                Controls.Add(chip);
                cx += 90;
            }

            // 搜索框
            _search = new RoundField
            {
                Bounds = new Rectangle(cx + 16, 89, 220, 34),
                PlaceholderText = "搜索版本号，如 1.20.1",
                Font = Theme.SmallFont
            };
            _search.TextChanged += (s, e) => ApplyFilter();
            Controls.Add(_search);

            var hint = new Label
            {
                Text = "共 ？ 个版本",
                BackColor = Color.Transparent,
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                Location = new Point(cx + 260, 95),
                AutoSize = true,
                Name = "countHint"
            };
            Controls.Add(hint);

            // 卡片流
            _flow = new FlowLayoutPanel
            {
                Bounds = new Rectangle(32, 134, 976, 546),
                AutoScroll = true,
                BackColor = Color.Transparent,
                WrapContents = true
            };
            Controls.Add(_flow);

            // 加载遮罩：盖在内容区上，转 60fps 的环
            _loading = new LoadingOverlay("正在获取版本清单 ...");
            _loading.Bounds = new Rectangle(32, 134, 976, 546);
            Controls.Add(_loading);
            _loading.BringToFront();
        }

        private LoadingOverlay _loading;

        private async Task LoadAsync()
        {
            _main.SetStatus("正在获取版本清单 ...");
            _loading.Caption = "正在获取版本清单 ...";
            _loading.Visible = true;
            try
            {
                _versions = await Task.Run(() => VersionManifest.GetVersions());
                ApplyFilter();
                _main.SetStatus("版本清单已加载，共 " + _versions.Count + " 个版本");
            }
            catch (Exception ex)
            {
                _loading.Caption = "加载失败：" + ex.Message;
                _loading.Spinner.Stop();
                _main.SetStatus("获取版本清单失败：" + ex.Message);
                return;
            }
            _loading.Visible = false;
        }

        private void ApplyFilter()
        {
            if (_versions == null) return;
            _flow.Controls.Clear();

            IEnumerable<VersionManifest.VersionEntry> q = _versions;
            if (_chips[1].Selected) q = q.Where(v => v.Type == "release");
            else if (_chips[2].Selected) q = q.Where(v => v.Type == "snapshot");
            else if (_chips[3].Selected) q = q.Where(v => v.Type == "old_beta");
            else if (_chips[4].Selected) q = q.Where(v => v.Type == "old_alpha");

            var keyword = _search.Text.Trim();
            if (keyword.Length > 0) q = q.Where(v => v.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            var shown = q.Take(300).ToList();
            foreach (var v in shown)
            {
                var card = new VersionCard
                {
                    VersionId = v.Id,
                    VersionType = v.Type,
                    TypeCn = v.TypeCn,
                    DateStr = v.ReleaseTime.Length >= 10 ? v.ReleaseTime.Substring(0, 10) : ""
                };
                var id = v.Id;
                card.OnInstall += () => Install(id);
                _flow.Controls.Add(card);
            }

            var hint = Controls.Find("countHint", true).FirstOrDefault() as Label;
            if (hint != null)
                hint.Text = "显示 " + shown.Count + " / " + _versions.Count + " 个版本";
        }

        private void Install(string id)
        {
            // PCL 式：弹窗选加载器（原版 / Fabric / Quilt / NeoForge / Forge）
            using (var dlg = new InstallPickForm(_main, id))
                dlg.ShowDialog(FindForm());
        }
    }
}
