using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EasyerLauncher.Pages
{
    /// <summary>
    /// 任务中心：点击底部状态栏打开。
    /// 默认视图 = 总进度条 + XX%；下方是各部分明细（游戏下载 / 加载器 / 模组 / 整合包 / 游戏加载）+ 历史记录。
    /// </summary>
    public class TaskCenterPage : Panel
    {
        private readonly MainForm _main;
        private RoundProgressBar _total;
        private Label _totalPct;
        private Label _summary;
        private FlowLayoutPanel _list;
        private System.Windows.Forms.Timer _refresh;

        public TaskCenterPage(MainForm main)
        {
            _main = main;
            Dock = DockStyle.Fill;
            Build();

            // 100ms 刷新一次（进度环那边推数据，这里只管画）
            _refresh = new System.Windows.Forms.Timer { Interval = 100 };
            _refresh.Tick += (s, e) => RefreshBoard();
            VisibleChanged += (s, e) =>
            {
                if (IsDisposed) return;
                if (Visible) { RefreshBoard(); _refresh.Start(); }
                else _refresh.Stop();
            };
            HandleCreated += (s, e) => { if (Visible) { RefreshBoard(); _refresh.Start(); } };
            Disposed += (s, e) => { try { _refresh?.Stop(); _refresh?.Dispose(); } catch { } };
            RefreshBoard();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            if (_list != null)
                _list.Bounds = new Rectangle(32, 258, Math.Max(500, Width - 64), Math.Max(200, Height - 288));
        }

        private void Build()
        {
            Controls.Add(PageUtil.Title("任务中心"));

            // ===== 总进度卡 =====
            var totalCard = new W11Card { Bounds = new Rectangle(32, 90, 976, 140) };
            Controls.Add(totalCard);

            totalCard.Controls.Add(new Label
            {
                Text = "总进度",
                Font = new Font(Theme.DefaultFontFamily, 12f, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = Theme.Foreground,
                Location = new Point(24, 20),
                AutoSize = true
            });

            _totalPct = new Label
            {
                Text = "0%",
                Font = new Font(Theme.DefaultFontFamily, 18f, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = Theme.Accent,
                Location = new Point(860, 18),
                AutoSize = true
            };
            totalCard.Controls.Add(_totalPct);

            _total = new RoundProgressBar
            {
                Bounds = new Rectangle(24, 70, 928, 14)
            };
            totalCard.Controls.Add(_total);

            _summary = new Label
            {
                Text = "暂无进行中的任务。下载、安装、启动时，这里会实时显示每一部分的进度。",
                Font = Theme.SmallFont,
                BackColor = Color.Transparent,
                ForeColor = Theme.ForegroundDim,
                Location = new Point(24, 98),
                AutoSize = true
            };
            totalCard.Controls.Add(_summary);

            // ===== 明细列表 =====
            _list = new FlowLayoutPanel
            {
                Bounds = new Rectangle(32, 258, 976, 502),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            Controls.Add(_list);
        }

        private readonly Dictionary<MainForm.TaskItem, Row> _rows = new();
        private Label _empty;

        private sealed class Row
        {
            public W11Card Card;
            public W11Button Chip;
            public Label Name, Msg, Pct;
            public RoundProgressBar Bar;
        }

        private void RefreshBoard()
        {
            var tasks = _main.Tasks;
            var shown = tasks.Take(14).ToList();
            var running = tasks.Where(t => t.Running).ToList();

            // ---- 总进度：进行中任务的均值；任一不确定 → 总条也走不确定动画 ----
            if (running.Count == 0)
            {
                _total.Indeterminate = false;
                _total.Value = 0f;
                _totalPct.Text = tasks.Count > 0 ? "空闲" : "0%";
                _summary.Text = tasks.Count == 0
                    ? "暂无进行中的任务。下载、安装、启动时，这里会实时显示每一部分的进度。"
                    : "全部任务已完成 ✓";
            }
            else
            {
                bool anyIndet = running.Any(t => t.Progress < 0f);
                if (anyIndet)
                {
                    _total.Indeterminate = true;
                    _totalPct.Text = running.Count + " 个进行中";
                }
                else
                {
                    _total.Indeterminate = false;
                    float avg = running.Average(t => t.Progress);
                    _total.Value = avg;
                    _totalPct.Text = (int)Math.Round(avg * 100f) + "%";
                }
                _summary.Text = running.Count == 1
                    ? running[0].CurrentMsg
                    : running.Count + " 个任务进行中：" + string.Join("、", running.Select(t => t.Category).Distinct());
            }

            // ---- 明细：就地更新已有行，只增删变化的行（每 100ms 重建会让文字闪）----
            var stale = _rows.Keys.Where(t => !shown.Contains(t)).ToList();
            foreach (var t in stale) _rows.Remove(t);
            for (int i = _list.Controls.Count - 1; i >= 0; i--)
            {
                if (_list.Controls[i] is W11Card c &&
                    !_rows.Values.Any(r => ReferenceEquals(r.Card, c)))
                {
                    _list.Controls.RemoveAt(i);
                    c.Dispose();
                }
            }

            foreach (var t in shown)
            {
                if (!_rows.TryGetValue(t, out var row))
                {
                    row = MakeRow(t);
                    _rows[t] = row;
                    // 插到正确位置（保持任务板顺序）
                    int idx = shown.IndexOf(t);
                    int ctrlIdx = Math.Min(idx, _list.Controls.Count);
                    _list.Controls.Add(row.Card);
                    _list.Controls.SetChildIndex(row.Card, ctrlIdx);
                }
                UpdateRow(row, t);
            }

            // ---- 空状态 ----
            if (tasks.Count == 0 && _empty == null)
            {
                _empty = new Label
                {
                    Text = "还没有任务记录。\n\n· 下载游戏 / 加载器 / 整合包 / 模组\n· 启动游戏\n\n进行时都会出现在这里，并实时刷新进度。",
                    ForeColor = Theme.ForegroundDim,
                    BackColor = Color.Transparent,
                    Font = Theme.BodyFont,
                    AutoSize = true,
                    Padding = new Padding(16, 24, 16, 8)
                };
                _list.Controls.Add(_empty);
            }
            else if (tasks.Count > 0 && _empty != null)
            {
                _list.Controls.Remove(_empty);
                _empty.Dispose();
                _empty = null;
            }
        }

        private static void UpdateRow(Row r, MainForm.TaskItem t)
        {
            r.Chip.Selected = t.Running;
            r.Chip.Invalidate();
            r.Name.Text = t.Name;
            r.Name.ForeColor = t.Running ? Theme.Foreground : Theme.ForegroundDim;
            r.Msg.Text = t.Running ? t.CurrentMsg : "完成 ✓  " + (t.EndedAt?.ToString("HH:mm:ss") ?? "");
            r.Bar.Indeterminate = t.Running && t.Progress < 0f;
            if (!(t.Running && t.Progress < 0f)) r.Bar.Value = Math.Max(0f, t.Progress);
            r.Pct.Text = t.Running ? (t.Progress < 0f ? "…" : (int)Math.Round(t.Progress * 100f) + "%") : "100%";
            r.Pct.ForeColor = t.Running ? Theme.Accent : Theme.ForegroundDim;
        }

        private Row MakeRow(MainForm.TaskItem t)
        {
            var card = new W11Card { Size = new Size(_list.Width - 24, 74), Margin = new Padding(0, 0, 0, 8) };
            var row = new Row { Card = card };

            // 分类徽章
            var cat = new W11Button
            {
                Text = t.Category,
                ButtonKind = W11Button.Kind.Chip,
                Selected = t.Running,
                Font = Theme.SmallFont,
                Bounds = new Rectangle(16, 20, 76, 26)
            };
            cat.Enabled = false;
            card.Controls.Add(cat);
            row.Chip = cat;

            // 名称 + 当前消息
            var name = new Label
            {
                Text = t.Name,
                Font = new Font(Theme.DefaultFontFamily, 9.5f, FontStyle.Bold),
                BackColor = Color.Transparent,
                ForeColor = t.Running ? Theme.Foreground : Theme.ForegroundDim,
                Location = new Point(106, 12),
                AutoSize = false,
                Size = new Size(card.Width - 220, 20),
                AutoEllipsis = true
            };
            card.Controls.Add(name);
            row.Name = name;

            var msg = new Label
            {
                Text = t.Running ? t.CurrentMsg : "完成 ✓  " + (t.EndedAt?.ToString("HH:mm:ss") ?? ""),
                Font = Theme.SmallFont,
                BackColor = Color.Transparent,
                ForeColor = Theme.ForegroundDim,
                Location = new Point(106, 33),
                AutoSize = false,
                Size = new Size(card.Width - 220, 17),
                AutoEllipsis = true
            };
            card.Controls.Add(msg);
            row.Msg = msg;

            // 小进度条 + 百分比
            var bar = new RoundProgressBar
            {
                Bounds = new Rectangle(106, 54, card.Width - 240, 10),
                Value = t.Progress
            };
            if (t.Running && t.Progress < 0f) bar.Indeterminate = true;
            card.Controls.Add(bar);
            row.Bar = bar;

            var pct = new Label
            {
                Text = t.Running ? (t.Progress < 0f ? "…" : (int)Math.Round(t.Progress * 100f) + "%") : "100%",
                Font = Theme.SmallFont,
                BackColor = Color.Transparent,
                ForeColor = t.Running ? Theme.Accent : Theme.ForegroundDim,
                Location = new Point(card.Width - 120, 48),
                AutoSize = true
            };
            card.Controls.Add(pct);
            row.Pct = pct;

            return row;
        }
    }
}
