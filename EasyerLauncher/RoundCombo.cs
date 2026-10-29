using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 圆角选择框：外观与 RoundField 一致（自绘圆角边框 + Input 底色，聚焦变强调色）。
    /// 真正的 ComboBox 藏在自绘封面下面（原生 ComboBox 的 chrome 压不住，Region 也裁不掉），
    /// 点击封面时让它弹出下拉列表 —— 选项正好出现在框的下方。
    /// </summary>
    public class RoundCombo : Control
    {
        public readonly ComboBox Combo;
        private readonly Cover _cover;
        private bool _hover;

        public RoundCombo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            Combo = new ComboBox
            {
                BackColor = Theme.Input,
                ForeColor = Theme.Foreground,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = Theme.BodyFont
            };
            Controls.Add(Combo);

            _cover = new Cover(this);
            Controls.Add(_cover);   // 后添加 → 画在 Combo 上面，把它完全盖住

            Combo.SelectedIndexChanged += (s, e) => { _cover.Invalidate(); SelectedIndexChanged?.Invoke(this, e); };
            Combo.GotFocus += (s, e) => _cover.Invalidate();
            Combo.LostFocus += (s, e) => _cover.Invalidate();

            Theme.Changed += OnThemeChanged;
            Disposed += (s, e) => Theme.Changed -= OnThemeChanged;
            OnThemeChanged();
        }

        private void OnThemeChanged()
        {
            if (Combo == null || Combo.IsDisposed) return;
            Combo.BackColor = Theme.Input;
            Combo.ForeColor = Theme.Foreground;
            _cover.Invalidate();
        }

        // ---- 代理：替换旧 ComboBox 时调用方不用改 ----
        public ComboBox.ObjectCollection Items => Combo.Items;
        public object SelectedItem { get => Combo.SelectedItem; set { Combo.SelectedItem = value; _cover.Invalidate(); } }
        public int SelectedIndex { get => Combo.SelectedIndex; set { Combo.SelectedIndex = value; _cover.Invalidate(); } }
        public event EventHandler SelectedIndexChanged;

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Relayout();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Relayout();
        }

        private void Relayout()
        {
            // Combo 撑满内部（被封面盖住看不见），下拉弹层从框底弹出
            Combo.Bounds = new Rectangle(0, 0, Width, Height);
            _cover.Bounds = new Rectangle(0, 0, Width, Height);
        }

        private void OpenDropDown()
        {
            if (!Combo.IsHandleCreated) _ = Combo.Handle;   // 强制建句柄，否则 DroppedDown 无效
            Combo.Focus();
            Combo.DroppedDown = true;
        }

        /// <summary>封面：盖住原生 ComboBox，自绘圆角外观 + 选中文字 + 箭头。</summary>
        private sealed class Cover : Control
        {
            private readonly RoundCombo _owner;
            private bool _hover;

            public Cover(RoundCombo owner)
            {
                _owner = owner;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;

                Click += (s, e) => _owner.OpenDropDown();
                MouseEnter += (s, e) => { _hover = true; Invalidate(); };
                MouseLeave += (s, e) => { _hover = false; Invalidate(); };
            }

            protected override bool IsInputKey(Keys keyData) =>
                keyData is Keys.Enter or Keys.Down or Keys.Up or Keys.F4 || base.IsInputKey(keyData);

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode is Keys.Enter or Keys.Down or Keys.F4) { _owner.OpenDropDown(); e.Handled = true; }
                base.OnKeyDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.BackdropOf(this));

                var focused = _owner.Combo.Focused;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = Theme.RoundedPath(r, 9);
                using (var b = new SolidBrush(Theme.Input)) g.FillPath(b, path);
                using (var pen = new Pen(focused ? Theme.Accent : Theme.Border, focused ? 1.6f : 1f))
                    g.DrawPath(pen, path);

                // 选中项文字
                var text = _owner.Combo.SelectedItem?.ToString() ?? "";
                if (text.Length > 0)
                {
                    var tr = new Rectangle(12, 0, Width - 42, Height);
                    TextRenderer.DrawText(g, text, _owner.Combo.Font, tr, Theme.Foreground,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                // 下拉箭头（实心小三角）
                int cx = Width - 15, cy = Height / 2;
                using var br = new SolidBrush(_hover ? Theme.Accent : Theme.ForegroundDim);
                g.FillPolygon(br, new[]
                {
                    new Point(cx - 5, cy - 3),
                    new Point(cx + 5, cy - 3),
                    new Point(cx, cy + 3)
                });
            }
        }
    }
}
