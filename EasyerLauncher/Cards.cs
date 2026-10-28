using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 图形化的版本选择器：一排卡片网格，单击选中（代替下拉框和列表）。
    /// 用于加载器版本、整合包版本、安装选项等所有"选一个"的场景。
    /// </summary>
    public class CardPicker : Panel
    {
        public class Item
        {
            public string Title = "";
            public string Sub = "";
            public string Badge = "";     // 右上角小标签，如"推荐"
            public string IconKind = "";  // 左侧徽标：vanilla/fabric/quilt/forge/neoforge/pack，空=不画
            public object Tag;
        }

        private readonly FlowLayoutPanel _flow;
        private readonly List<Card> _cards = new();
        private List<Item> _items = new();
        private int _selected = -1;

        /// <summary>卡片尺寸（可调）。</summary>
        public Size CardSize = new Size(150, 62);

        public event Action SelectionChanged;

        public int SelectedIndex => _selected;
        public Item SelectedItem =>
            _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

        public CardPicker()
        {
            BackColor = Color.Transparent;
            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                WrapContents = true,
                Padding = new Padding(0)
            };
            Controls.Add(_flow);
        }

        public void SetItems(IEnumerable<Item> items)
        {
            _flow.SuspendLayout();
            foreach (var c in _cards) { _flow.Controls.Remove(c); c.Dispose(); }
            _cards.Clear();
            _items = new List<Item>(items ?? new List<Item>());
            _selected = _items.Count > 0 ? 0 : -1;

            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                var card = new Card(_items[i])
                {
                    Size = CardSize,
                    Margin = new Padding(0, 0, 8, 8),
                    Selected = i == _selected
                };
                card.Click += (s, e) => Select(idx);
                _cards.Add(card);
                _flow.Controls.Add(card);
            }
            _flow.ResumeLayout(true);
            SelectionChanged?.Invoke();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _cards.Count) return;
            _selected = index;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Selected = i == index;
                _cards[i].Invalidate();
            }
            SelectionChanged?.Invoke();
        }

        public void Clear() => SetItems(null);

        /// <summary>单张卡片：标题 + 副标题，选中用强调色描边。</summary>
        private class Card : Control
        {
            private readonly Item _item;
            private bool _hover;

            public bool Selected;

            public Card(Item item)
            {
                _item = item;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);
                Cursor = Cursors.Hand;
                BackColor = Color.Transparent;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.BackdropOf(this));

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = Theme.RoundedPath(rect, 10))
                {
                    var fill = Selected ? Theme.Lerp(Theme.Card, Theme.Accent, 0.22f)
                             : _hover ? Theme.CardHover : Theme.Card;
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var pen = new Pen(Selected ? Theme.Accent : Theme.Border)) g.DrawPath(pen, path);
                }

                // 左侧类型徽标（草方块 / 加载器 logo / 整合包）
                int textLeft = 12;
                if (!string.IsNullOrEmpty(_item.IconKind))
                {
                    int sz = Math.Min(28, Height - 14);
                    var ir = new Rectangle(10, (Height - sz) / 2, sz, sz);
                    Icons.DrawKind(g, _item.IconKind, ir);
                    textLeft = ir.Right + 10;
                }

                bool hasSub = !string.IsNullOrEmpty(_item.Sub);
                int textTop = hasSub ? 12 : (Height - 20) / 2;
                int textW = Math.Max(20, Width - textLeft - 12);
                TextRenderer.DrawText(g, _item.Title,
                    new Font(Theme.DefaultFontFamily, 10f, FontStyle.Bold),
                    new Rectangle(textLeft, textTop, textW, 20),
                    Selected ? Theme.Accent : Theme.Foreground,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (hasSub)
                    TextRenderer.DrawText(g, _item.Sub, Theme.SmallFont,
                        new Rectangle(textLeft, textTop + 21, textW, 17),
                        Theme.ForegroundDim,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // 右上角徽标
                if (!string.IsNullOrEmpty(_item.Badge))
                {
                    var label = _item.Badge;
                    var sz = TextRenderer.MeasureText(label, Theme.SmallFont);
                    var badge = new Rectangle(Width - sz.Width - 18, 6, sz.Width + 10, 18);
                    using (var bp = Theme.RoundedPath(badge, 9))
                    using (var bb = new SolidBrush(Color.FromArgb(46, Theme.Accent)))
                        g.FillPath(bb, bp);
                    TextRenderer.DrawText(g, label, Theme.SmallFont, badge, Theme.Accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    /// <summary>一排可换行的选择胶囊（用于游戏版本等短列表）。</summary>
    public class ChipPicker : Panel
    {
        private readonly FlowLayoutPanel _flow;
        private readonly List<W11Button> _chips = new();
        private int _selected = -1;

        public event Action SelectionChanged;

        public string SelectedItem =>
            _selected >= 0 && _selected < _chips.Count ? _chips[_selected].Text : null;

        public ChipPicker()
        {
            BackColor = Color.Transparent;
            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                WrapContents = true
            };
            Controls.Add(_flow);
        }

        public void SetItems(IEnumerable<string> items)
        {
            _flow.SuspendLayout();
            foreach (var c in _chips) { _flow.Controls.Remove(c); c.Dispose(); }
            _chips.Clear();

            int i = 0;
            foreach (var text in items)
            {
                int idx = i;
                var chip = new W11Button
                {
                    Text = text,
                    ButtonKind = W11Button.Kind.Chip,
                    Font = Theme.SmallFont,
                    Height = 32,
                    Width = Math.Max(76, TextRenderer.MeasureText(text, Theme.SmallFont).Width + 34),
                    Margin = new Padding(0, 0, 8, 8),
                    Selected = i == 0
                };
                chip.Click += (s, e) => Select(idx);
                _chips.Add(chip);
                _flow.Controls.Add(chip);
                i++;
            }
            _selected = _chips.Count > 0 ? 0 : -1;
            _flow.ResumeLayout(true);
            SelectionChanged?.Invoke();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _chips.Count) return;
            _selected = index;
            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].Selected = i == index;
                _chips[i].Invalidate();
            }
            SelectionChanged?.Invoke();
        }
    }
}
