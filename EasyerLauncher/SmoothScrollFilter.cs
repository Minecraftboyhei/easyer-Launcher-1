using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 全局平滑滚动：拦截鼠标滚轮 / 触控板两指滑动，把"一格猛跳三行"换成缓动动画。
    /// 只接管主窗体内的 AutoScroll 容器，弹窗一律放行；连续滚动平滑续接（动画中途只改目标，不重启）。
    /// </summary>
    public sealed class SmoothScrollFilter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point p);

        private readonly Control _root;
        private float _toY;
        private Anim.Handle _h;
        private ScrollableControl _chasing;

        public SmoothScrollFilter(Control root) { _root = root; }

        /// <summary>诊断用：跳过消息与光标，直接对指定容器施加一次滚动增量。</summary>
        internal void Probe(ScrollableControl c, int delta) => ScrollBy(c, delta);

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL && m.Msg != WM_MOUSEHWHEEL) return false;

            var under = Control.FromHandle(WindowFromPoint(Cursor.Position));
            if (under == null) return false;
            if (!ReferenceEquals(under.TopLevelControl, _root)) return false;   // 弹窗/对话框不接管

            var sc = FindScrollable(under);
            if (sc == null) return false;

            int delta = (short)((long)m.WParam >> 16);
            if (m.Msg == WM_MOUSEHWHEEL) ScrollX(sc, delta);
            else ScrollBy(sc, delta);
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

        /// <summary>鼠标一格 = ±120；精密触控板发的是一串小增量，必须用浮点换算（整数除法会把它们全吞成 0）。</summary>
        private const float WHEEL_DELTA = 120f;
        private const float StepPx = 3f * 46f;      // 一格 ≈ 3 行 ≈ 138px（对齐卡片行高）

        private void ScrollBy(ScrollableControl c, int wheelDelta)
        {
            // 正 delta（滚轮向前推 / 触控板向上滑）= 看下方内容 = 滚动位置增加（Windows 标准方向）
            float px = (wheelDelta / WHEEL_DELTA) * StepPx;
            float cur = -c.AutoScrollPosition.Y;
            float max = Math.Max(0, c.DisplayRectangle.Height - c.ClientSize.Height);

            // 动画正在追同一个容器 → 只把目标往上累加，让它自己追过去。
            // 旧实现在这里 Stop 再重启，触控板每秒几十条消息会让动画永远从 t=0 重来 —— 粘住不动。
            bool chasing = _h != null && _h.Running && ReferenceEquals(_chasing, c);
            float from = chasing ? _toY : cur;
            float target = Math.Max(0f, Math.Min(max, from + px));
            if (!chasing && Math.Abs(target - cur) < 0.5f) return;

            _toY = target;
            if (chasing) return;      // 交给在跑的动画追新目标
            StartChase(c);
        }

        private void ScrollX(ScrollableControl c, int wheelDelta)
        {
            float max = Math.Max(0, c.DisplayRectangle.Width - c.ClientSize.Width);
            if (max <= 0) return;
            float px = (wheelDelta / WHEEL_DELTA) * StepPx;
            float cur = -c.AutoScrollPosition.X;
            float target = Math.Max(0f, Math.Min(max, cur + px));
            if (Math.Abs(target - cur) < 0.5f) return;
            float from = cur;
            Anim.Run(190, Ease.OutCubic, t =>
            {
                if (c.IsDisposed) return;
                try { c.AutoScrollPosition = new Point((int)Math.Round(from + (target - from) * t), -c.AutoScrollPosition.Y); }
                catch { }
            }, null, c);
        }

        /// <summary>朝 _toY 追过去；追完若目标又被刷新了（触控板还在滑），自动接力再追。</summary>
        private void StartChase(ScrollableControl c)
        {
            _chasing = c;
            float from = -c.AutoScrollPosition.Y;
            _h = Anim.Run(190, Ease.OutCubic, t =>
            {
                if (c.IsDisposed) return;
                try { c.AutoScrollPosition = new Point(0, (int)Math.Round(from + (_toY - from) * t)); }
                catch { }
            }, () =>
            {
                if (!ReferenceEquals(_chasing, c) || c.IsDisposed) return;
                _h = null;
                float now = -c.AutoScrollPosition.Y;
                // 还差 0.5px 以上 = 中途又来了新增量（触控板连续滑动的典型情况）→ 接着追
                if (Math.Abs(_toY - now) > 0.5f) StartChase(c);
                else _chasing = null;
            }, c);
        }
    }
}
