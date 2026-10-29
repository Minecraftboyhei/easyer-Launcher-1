using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 旋转加载环（60fps，由全局动画泵驱动）。加载数据时盖在内容区上，
    /// 转弧 + 拖尾，Win11 味道。
    /// </summary>
    public class LoadingSpinner : Control
    {
        private float _angle;
        private Anim.Handle _h;
        private bool _spinning;

        /// <summary>转速（度/秒）。</summary>
        public float Speed = 330f;
        /// <summary>弧线粗细。</summary>
        public float Thickness = 4f;
        /// <summary>环下底色（浅色轨道）。</summary>
        public bool ShowTrack = true;

        public LoadingSpinner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(48, 48);
        }

        public void Start()
        {
            if (_spinning) return;
            _spinning = true;
            Visible = true;
            _h = Anim.Loop(dt =>
            {
                if (IsDisposed) return false;
                _angle = (_angle + Speed * (float)dt) % 360f;
                Invalidate();
                return true;
            }, this);
        }

        public void Stop()
        {
            _spinning = false;
            _h?.Stop();
            _h = null;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 挂到页面上 Visible=true 即自动开转，不用每个调用点手动 Start
            if (Visible && !_spinning && IsHandleCreated) Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Visible && !_spinning) Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            float pad = Thickness + 2f;
            var r = new RectangleF(pad, pad, Width - pad * 2, Height - pad * 2);
            if (r.Width < 4) return;

            if (ShowTrack)
            {
                using var pen = new Pen(Theme.Border, Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, r.X, r.Y, r.Width, r.Height, 0, 360);
            }

            using (var pen = new Pen(Theme.Accent, Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                // 主弧 270°，再叠一段 40° 短弧当拖尾
                g.DrawArc(pen, r.X, r.Y, r.Width, r.Height, _angle, 275f);
                using var tail = new Pen(Color.FromArgb(120, Theme.Accent), Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(tail, r.X, r.Y, r.Width, r.Height, _angle - 52f, 40f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Stop();
            base.Dispose(disposing);
        }
    }

    /// <summary>页面内容区的加载遮罩：居中转环 + 一行说明文字。Visible = true 即开转。</summary>
    public class LoadingOverlay : Panel
    {
        public readonly LoadingSpinner Spinner;
        private readonly Label _text;

        public LoadingOverlay(string caption = "加载中 ...")
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            Spinner = new LoadingSpinner { Size = new Size(52, 52) };
            Controls.Add(Spinner);

            _text = new Label
            {
                Text = caption,
                Font = Theme.BodyFont,
                ForeColor = Theme.ForegroundDim,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            Controls.Add(_text);

            Resize += (s, e) => Relayout();
            Theme.Changed += () => { if (!IsDisposed) { _text.ForeColor = Theme.ForegroundDim; Invalidate(); } };
        }

        public string Caption
        {
            get => _text.Text;
            set { _text.Text = value; Relayout(); }
        }

        private void Relayout()
        {
            int cy = Height / 2 - 40;
            Spinner.Location = new Point(Width / 2 - Spinner.Width / 2, Math.Max(8, cy));
            _text.Location = new Point(Width / 2 - _text.Width / 2, Math.Max(8, cy) + Spinner.Height + 10);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 罩一层父底色，把下面还没加载好的空白盖住
            var g = e.Graphics;
            g.Clear(Theme.BackdropOf(this));
        }
    }
}
