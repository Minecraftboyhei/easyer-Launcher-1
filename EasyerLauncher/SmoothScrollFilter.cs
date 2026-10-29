using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 全局平滑滚动：拦截滚轮，把"一格猛跳三行"的生硬滚动换成 190ms 缓动动画。
    /// 只接管主窗体内的 AutoScroll 容器，弹窗一律放行；连续滚动会平滑续接（动画中途重设目标）。
    /// </summary>
    public sealed class SmoothScrollFilter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point p);

        private readonly Control _root;
        private float _toY;
        private Anim.Handle _h;

        public SmoothScrollFilter(Control root) { _root = root; }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;

            var under = Control.FromHandle(WindowFromPoint(Cursor.Position));
            if (under == null) return false;
            if (!ReferenceEquals(under.TopLevelControl, _root)) return false;   // 弹窗/对话框不接管

            var sc = FindScrollable(under);
            if (sc == null) return false;

            int delta = (short)((long)m.WParam >> 16);
            ScrollBy(sc, delta);
            return true;   // 吞掉系统默认滚动
        }

        private static ScrollableControl FindScrollable(Control c)
        {
            for (var x = c; x != null; x = x.Parent)
            {
                // 这些控件自己会处理滚轮，别抢
                if (x is TextBox || x is ComboBox || x is ListBox || x is NumericUpDown) return null;
                if (x is ScrollableControl s && s.AutoScroll && s.DisplayRectangle.Height > s.ClientSize.Height + 2)
                    return s;
            }
            return null;
        }

        private void ScrollBy(ScrollableControl c, int wheelDelta)
        {
            float px = -(wheelDelta / 120) * 3 * 46f;     // 一格 ≈ 3 行 ≈ 138px（对齐卡片行高）
            int cur = -c.AutoScrollPosition.Y;
            int max = Math.Max(0, c.DisplayRectangle.Height - c.ClientSize.Height);
            float from = cur;
            float target = Math.Max(0f, Math.Min(max, (_h != null && _h.Running ? _toY : cur) + px));
            if (Math.Abs(target - cur) < 0.5f) return;

            _toY = target;
            _h?.Stop();
            _h = Anim.Run(190, Ease.OutCubic, t =>
            {
                if (c.IsDisposed) return;
                try { c.AutoScrollPosition = new Point(0, (int)Math.Round(from + (_toY - from) * t)); }
                catch { }
            }, null, c);
        }
    }
}
