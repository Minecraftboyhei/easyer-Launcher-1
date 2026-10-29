using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 侧边栏容器：双缓冲自绘，负责画「滑动选中药丸」。
    /// 药丸画在父级 OnPaint 里，导航按钮的 BackColor 设成 Transparent，
    /// 于是按钮只画态自己那一层，底下的药丸就能透出来。
    /// </summary>
    public class SidebarPanel : Panel
    {
        public bool PillVisible;
        public float PillTop;
        public float PillHeight = 44f;
        /// <summary>强调条淡入淡出（切页时闪一下）。</summary>
        public float AccentAlpha = 1f;

        public SidebarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
        }

        private Rectangle PillRect()
        {
            int h = (int)Math.Round(PillHeight);
            return new Rectangle(14, (int)Math.Round(PillTop), Math.Max(10, Width - 28), h);
        }

        /// <summary>只重绘药丸新旧位置的最小并集，别整块侧边栏刷。</summary>
        public void MovePill(float top, float height)
        {
            if (!PillVisible) { PillTop = top; PillHeight = height; return; }
            var old = PillRect();
            PillTop = top;
            PillHeight = height;
            var neu = PillRect();
            var r = Rectangle.Union(old, neu);
            r.Inflate(6, 6);
            if (IsHandleCreated) Invalidate(r);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(BackColor)) g.FillRectangle(bg, ClientRectangle);

            if (!PillVisible) return;

            var r = PillRect();
            if (r.Height <= 2) return;
            var rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (var path = Theme.RoundedPath(rr, rr.Height / 2))
            {
                using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(26, Theme.Accent)))
                    g.DrawPath(pen, path);
            }

            // 左侧强调条
            if (AccentAlpha > 0.02f)
            {
                var bar = new Rectangle(r.X + 4, r.Y + r.Height / 2 - 8, 3, 16);
                using (var bp = Theme.RoundedPath(bar, 2))
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * AccentAlpha), Theme.Accent)))
                    g.FillPath(b, bp);
            }
        }
    }
}
