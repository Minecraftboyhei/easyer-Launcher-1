using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>圆角进度条。Indeterminate 模式 = 一段滑块来回跑（60fps）。</summary>
    public class RoundProgressBar : Control
    {
        private float _value;
        private bool _indeterminate;
        private float _phase;
        private Anim.Handle _h;

        public RoundProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(300, 12);
        }

        /// <summary>0..1。自动钳制。</summary>
        public float Value
        {
            get => _value;
            set { _value = Math.Max(0f, Math.Min(1f, value)); Invalidate(); }
        }

        public bool Indeterminate
        {
            get => _indeterminate;
            set
            {
                if (_indeterminate == value) return;
                _indeterminate = value;
                if (value)
                {
                    _h = Anim.Loop(dt =>
                    {
                        if (IsDisposed) return false;
                        _phase += (float)dt * 0.55f;
                        if (_phase > 1.35f) _phase = -0.35f;
                        Invalidate();
                        return true;
                    }, this);
                }
                else { _h?.Stop(); _h = null; }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            int h = Math.Min(Height, 12);
            var track = new Rectangle(0, (Height - h) / 2, Width - 1, h - 1);
            int radius = h / 2;
            using (var path = Theme.RoundedPath(track, radius))
            using (var b = new SolidBrush(Theme.Input))
                g.FillPath(b, path);

            if (_indeterminate)
            {
                float w = Math.Max(24f, Width * 0.28f);
                float x = _phase * (Width - w);
                var chunk = new RectangleF(x, (Height - h) / 2, w, h - 1);
                using var path2 = Theme.RoundedPath(new Rectangle((int)chunk.X, (int)chunk.Y, (int)chunk.Width, (int)chunk.Height), radius);
                using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, path2);
            }
            else if (_value > 0.005f)
            {
                int w = (int)((Width - 1) * _value);
                if (w >= h)
                {
                    using var path2 = Theme.RoundedPath(new Rectangle(0, (Height - h) / 2, w, h - 1), radius);
                    using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, path2);
                }
                else if (w > 0)
                {
                    using var b = new SolidBrush(Theme.Accent);
                    g.FillEllipse(b, 0, (Height - h) / 2, w, h - 1);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _h?.Stop(); _h = null; }
            base.Dispose(disposing);
        }
    }
}
