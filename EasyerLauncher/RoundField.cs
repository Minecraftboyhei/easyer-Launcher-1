using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 圆角输入框：自绘圆角边框 + Input 底色，内嵌无边框 TextBox（颜色与底色一致，四角天衣无缝）。
    /// 聚焦时边框变主题强调色。Text/PlaceholderText 等直接代理内部 Box，替换旧 TextBox 不用改调用方。
    /// </summary>
    public class RoundField : Panel
    {
        public readonly TextBox Box;
        private bool _focused;

        public RoundField(bool multiline = false, bool password = false, ScrollBars scrollBars = ScrollBars.None)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            Box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Input,
                ForeColor = Theme.Foreground,
                Multiline = multiline,
                UseSystemPasswordChar = password,
                ScrollBars = scrollBars,
                Font = Theme.BodyFont
            };
            Controls.Add(Box);
            if (multiline && Height < 60) Height = 80;

            Box.GotFocus += (s, e) => { _focused = true; Invalidate(); };
            Box.LostFocus += (s, e) => { _focused = false; Invalidate(); };
            Theme.Changed += OnThemeChanged;
            Disposed += (s, e) => Theme.Changed -= OnThemeChanged;
            OnThemeChanged();
        }

        private void OnThemeChanged()
        {
            if (Box == null || Box.IsDisposed) return;
            Box.BackColor = Theme.Input;
            Box.ForeColor = Theme.Foreground;
            Invalidate();
        }

        // ---- 代理：替换旧 TextBox 时调用方不用改 ----
        public override string Text { get => Box.Text; set => Box.Text = value; }
        public string PlaceholderText { get => Box.PlaceholderText; set => Box.PlaceholderText = value; }
        public bool ReadOnly { get => Box.ReadOnly; set => Box.ReadOnly = value; }
        public ScrollBars ScrollBars { set => Box.ScrollBars = value; }
        public void Clear() => Box.Clear();
        public void AppendText(string s) => Box.AppendText(s);
        public override Font Font { get => Box.Font; set { Box.Font = value; RelayoutBox(); } }
        public new event EventHandler TextChanged
        {
            add => Box.TextChanged += value;
            remove => Box.TextChanged -= value;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            RelayoutBox();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RelayoutBox();
        }

        private void RelayoutBox()
        {
            int pad = 10;
            if (Box.Multiline)
                Box.Bounds = new Rectangle(pad, 8, Math.Max(20, Width - pad * 2), Math.Max(20, Height - 16));
            else
                Box.Bounds = new Rectangle(pad, Math.Max(2, (Height - Box.Height) / 2),
                    Math.Max(20, Width - pad * 2), Box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedPath(r, 9);
            using (var b = new SolidBrush(Theme.Input)) g.FillPath(b, path);
            using (var pen = new Pen(_focused ? Theme.Accent : Theme.Border, _focused ? 1.6f : 1f))
                g.DrawPath(pen, path);
        }
    }
}
