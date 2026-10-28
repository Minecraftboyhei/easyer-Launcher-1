using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EasyerLauncher
{
    /// <summary>
    /// 旋转进度环：后台在跑（下载/安装/启动）时给个明确的图形化指示，
    /// 别让用户盯着一行状态栏文字猜到底是不是卡死了。
    /// </summary>
    public class ProgressRing : Control
    {
        private readonly System.Windows.Forms.Timer _t;
        private float _pct = -1f;          // <0 = 不确定（转圈），>=0 = 确定进度
        private int _spin;
        private string _caption = "";
        private bool _running;

        public ProgressRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(200, 24);
            Visible = false;
            _t = new System.Windows.Forms.Timer { Interval = 60 };
            _t.Tick += (s, e) =>
            {
                _spin = (_spin + 24) % 360;
                if (IsHandleCreated && !IsDisposed) Invalidate(new Rectangle(0, 0, 24, Height));
            };
        }

        /// <summary>开始转圈。pct 传 0~1 显示确定进度，传负数显示无限转圈。</summary>
        public void Start(string caption, float pct = -1f)
        {
            _caption = caption ?? "";
            _pct = pct;
            _running = true;
            if (!Visible) Visible = true;
            _t.Start();
            Invalidate();
        }

        public void Set(float pct, string caption = null)
        {
            _pct = pct;
            if (caption != null) _caption = caption;
            Invalidate();
        }

        public void Stop()
        {
            _running = false;
            _t.Stop();
            _pct = -1f;
            _caption = "";
            if (Visible) Visible = false;
            Invalidate();
        }

        public bool Running => _running;

        protected override void Dispose(bool disposing)
        {
            if (disposing) { try { _t.Stop(); _t.Dispose(); } catch { } }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_running) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            int d = Math.Min(18, Height - 4);
            var ring = new Rectangle(2, (Height - d) / 2, d, d);

            using (var bgPen = new Pen(Theme.Lerp(Theme.Border, Theme.Background, 0.3f), 2.6f))
                g.DrawEllipse(bgPen, ring);

            using (var fgPen = new Pen(Theme.Accent, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (_pct >= 0f)
                    g.DrawArc(fgPen, ring, -90, Math.Max(4, _pct * 360f));
                else
                    g.DrawArc(fgPen, ring, _spin, 110);
            }

            if (!string.IsNullOrEmpty(_caption))
            {
                var textRect = new Rectangle(d + 8, 0, Width - d - 8, Height);
                TextRenderer.DrawText(g, _caption, Theme.SmallFont, textRect, Theme.Foreground,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
