using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>主题引擎（PCL2 风格：大圆角、柔和边框、卡片化）。</summary>
    public static class Theme
    {
        // ===== 颜色令牌 =====
        public static Color Background;
        public static Color Sidebar;
        public static Color Card;
        public static Color CardHover;
        public static Color Foreground;
        public static Color ForegroundDim;
        public static Color Accent;
        public static Color AccentHover;
        public static Color AccentText;
        public static Color Border;
        public static Color Input;
        public static bool IsDark = true;

        public static string Mode = "dark";
        public static Color CustomAccent = ColorTranslator.FromHtml("#60A5FA");

        public static event Action Changed;

        public static Font TitleFont => new Font(DefaultFontFamily, 22f, FontStyle.Bold);
        public static Font H2Font => new Font(DefaultFontFamily, 14f, FontStyle.Bold);
        public static Font BodyFont => new Font(DefaultFontFamily, 9.75f);
        public static Font SmallFont => new Font(DefaultFontFamily, 8.75f);
        public static Font IconFont => new Font("Segoe MDL2 Assets", 13f);
        public static Font BigIconFont => new Font("Segoe MDL2 Assets", 22f);

        public static string DefaultFontFamily
        {
            get
            {
                try { using (var f = new Font("Segoe UI Variable Display", 10f)) { } return "Segoe UI Variable Display"; }
                catch { return "Microsoft YaHei UI"; }
            }
        }

        public static void Apply(string mode, string accentHex, string customBg, string customCard, string customFg)
        {
            Mode = mode;
            try { CustomAccent = ColorTranslator.FromHtml(accentHex); } catch { }

            if (mode == "light")
            {
                IsDark = false;
                Background = Color.FromArgb(246, 246, 250);
                Sidebar = Color.FromArgb(238, 238, 245);
                Card = Color.White;
                CardHover = Color.FromArgb(249, 250, 253);
                Foreground = Color.FromArgb(26, 26, 32);
                ForegroundDim = Color.FromArgb(110, 110, 120);
                Accent = CustomAccent;
                AccentHover = ControlPaint.Light(Accent, 0.15f);
                AccentText = Color.White;
                Border = Color.FromArgb(226, 226, 234);
                Input = Color.White;
            }
            else if (mode == "custom")
            {
                try
                {
                    Background = ColorTranslator.FromHtml(customBg);
                    Card = ColorTranslator.FromHtml(customCard);
                    Foreground = ColorTranslator.FromHtml(customFg);
                }
                catch
                {
                    Background = Color.FromArgb(27, 27, 31);
                    Card = Color.FromArgb(42, 42, 48);
                    Foreground = Color.FromArgb(242, 242, 247);
                }
                IsDark = Background.GetBrightness() < 0.5;
                Sidebar = ControlPaint.Dark(Background, 0.06f);
                CardHover = ControlPaint.Light(Card, 0.06f);
                ForegroundDim = IsDark ? Color.FromArgb(150, 150, 160) : Color.FromArgb(110, 110, 120);
                Accent = CustomAccent;
                AccentHover = ControlPaint.Light(Accent, 0.15f);
                AccentText = IsDark ? Color.White : Color.Black;
                Border = IsDark ? Color.FromArgb(58, 58, 66) : Color.FromArgb(226, 226, 234);
                Input = IsDark ? ControlPaint.Light(Background, 0.1f) : Color.White;
            }
            else
            {
                IsDark = true;
                Background = Color.FromArgb(30, 30, 34);
                Sidebar = Color.FromArgb(24, 24, 28);
                Card = Color.FromArgb(42, 42, 48);
                CardHover = Color.FromArgb(52, 52, 58);
                Foreground = Color.FromArgb(243, 243, 245);
                ForegroundDim = Color.FromArgb(155, 155, 165);
                Accent = CustomAccent;
                AccentHover = ControlPaint.Light(Accent, 0.15f);
                AccentText = Color.White;
                Border = Color.FromArgb(54, 54, 62);
                Input = Color.FromArgb(46, 46, 52);
            }

            Changed?.Invoke();
        }

        public static void ApplyTo(Control root)
        {
            root.BackColor = Background;
            root.ForeColor = Foreground;
            foreach (Control c in root.Controls)
            {
                if (c is W11Button || c is W11Card || c is ContentCard || c is VersionCard) { c.Invalidate(); continue; }
                if (c is TextBox || c is ComboBox || c is ListBox || c is NumericUpDown)
                {
                    c.BackColor = Input; c.ForeColor = Foreground; continue;
                }
                c.BackColor = Color.Transparent;
                c.ForeColor = Foreground;
                ApplyTo(c);
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void EnableWin11Window(Form form)
        {
            try
            {
                int dark = IsDark ? 1 : 0;
                DwmSetWindowAttribute(form.Handle, 20, ref dark, 4);
                int round = 2;
                DwmSetWindowAttribute(form.Handle, 33, ref round, 4);
                Color col = IsDark ? Color.FromArgb(30, 30, 34) : Color.FromArgb(246, 246, 250);
                int border = (col.R << 0) | (col.G << 8) | (col.B << 16);
                DwmSetWindowAttribute(form.Handle, 34, ref border, 4);
            }
            catch { }
        }

        public static readonly (string Name, string Hex)[] AccentPresets =
        {
            ("PCL 蓝", "#007ACC"),
            ("Win11 蓝", "#60A5FA"),
            ("活力紫", "#A78BFA"),
            ("青绿", "#34D399"),
            ("珊瑚", "#FB7185"),
            ("琥珀", "#FBBF24"),
        };

        // ===== 通用工具 =====
        internal static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Parse(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }

        /// <summary>类型徽章颜色（正式/快照/Beta/Alpha）。</summary>
        public static Color TypeColor(string type)
        {
            switch (type)
            {
                case "release": return Color.FromArgb(52, 211, 153);
                case "snapshot": return Color.FromArgb(251, 191, 36);
                case "old_beta": return Color.FromArgb(167, 139, 250);
                case "old_alpha": return Color.FromArgb(148, 163, 184);
                default: return ForegroundDim;
            }
        }
    }

    /// <summary>Win11/PCL 风格按钮（自绘，支持图标字形与选中态）。</summary>
    public class W11Button : Control
    {
        public enum Kind { Standard, Accent, Ghost, Nav, Chip }

        public Kind ButtonKind = Kind.Standard;
        public bool Selected = false;
        public string IconGlyph = "";
        public ContentAlignment MyTextAlign = ContentAlignment.MiddleCenter;
        private bool _hover;
        private bool _down;

        public W11Button()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Font = Theme.BodyFont;
            Cursor = Cursors.Hand;
            Height = 36;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = ButtonKind == Kind.Nav || ButtonKind == Kind.Chip ? Height / 2 : 8;
            using (var path = Theme.RoundedPath(rect, radius))
            {
                Color fill;
                Color text;

                switch (ButtonKind)
                {
                    case Kind.Accent:
                        fill = _down ? ControlPaint.Dark(Theme.Accent, 0.12f) : _hover ? Theme.AccentHover : Theme.Accent;
                        text = Theme.AccentText;
                        break;
                    case Kind.Ghost:
                        fill = _down ? Theme.CardHover : _hover ? Theme.Card : Color.Transparent;
                        text = Theme.Foreground;
                        break;
                    case Kind.Nav:
                        fill = Selected ? (_hover ? Theme.CardHover : Theme.Card) : _hover ? Theme.CardHover : Color.Transparent;
                        text = Selected ? Theme.Accent : Theme.Foreground;
                        break;
                    case Kind.Chip:
                        fill = Selected ? (_down ? ControlPaint.Dark(Theme.Accent, 0.12f) : _hover ? Theme.AccentHover : Theme.Accent)
                                        : _down ? Theme.CardHover : _hover ? Theme.Card : Theme.Input;
                        text = Selected ? Theme.AccentText : Theme.Foreground;
                        break;
                    default:
                        fill = _down ? Theme.CardHover : _hover ? Theme.Card : Theme.Input;
                        text = Theme.Foreground;
                        break;
                }

                if (fill != Color.Transparent)
                {
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                }
                if (ButtonKind == Kind.Standard || ButtonKind == Kind.Nav)
                {
                    using (var pen = new Pen(Selected ? Theme.Accent : Theme.Border))
                        g.DrawPath(pen, path);
                }

                // 布局：图标字形 + 文本
                var flags = MyTextAlign switch
                {
                    ContentAlignment.MiddleLeft => TextFormatFlags.Left | TextFormatFlags.VerticalCenter,
                    ContentAlignment.MiddleRight => TextFormatFlags.Right | TextFormatFlags.VerticalCenter,
                    _ => TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                } | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

                int iconW = 0;
                if (!string.IsNullOrEmpty(IconGlyph))
                {
                    using (var iconFont = new Font("Segoe MDL2 Assets", ButtonKind == Kind.Nav ? 12f : 10f))
                    {
                        iconW = TextRenderer.MeasureText(g, IconGlyph, iconFont, Size.Empty, TextFormatFlags.NoPadding).Width;
                        var iconRect = new Rectangle(
                            ButtonKind == Kind.Nav ? rect.X + Padding.Left : rect.X + (Width - iconW) / 2 - (Text.Length * 4),
                            rect.Y, iconW + 4, rect.Height);
                        // 图标和文本一起居中（Nav 左对齐时图标固定左侧）
                        if (ButtonKind == Kind.Nav)
                        {
                            TextRenderer.DrawText(g, IconGlyph, iconFont, iconRect, text,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        }
                        else
                        {
                            int textW = TextRenderer.MeasureText(Text, Font).Width;
                            int total = iconW + 6 + textW;
                            var ir = new Rectangle((Width - total) / 2, rect.Y, iconW + 4, rect.Height);
                            TextRenderer.DrawText(g, IconGlyph, iconFont, ir, text,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                            var tr = new Rectangle(ir.Right + 4, rect.Y, textW + 4, rect.Height);
                            TextRenderer.DrawText(g, Text, Font, tr, text,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        }
                    }
                }

                if (string.IsNullOrEmpty(IconGlyph) || ButtonKind == Kind.Nav)
                {
                    var textRect = rect;
                    if (!string.IsNullOrEmpty(IconGlyph) && ButtonKind == Kind.Nav)
                        textRect = new Rectangle(rect.X + Padding.Left + iconW + 8, rect.Y, rect.Width - Padding.Left - iconW - 12, rect.Height);
                    TextRenderer.DrawText(g, Text, Font, textRect, text, flags);
                }
            }
        }
    }

    /// <summary>大圆角卡片。</summary>
    public class W11Card : Panel
    {
        public W11Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Background);
            using (var path = Theme.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 12))
            {
                using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
                using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>带标题的大圆角卡片。</summary>
    public class W11CardWithTitle : W11Card
    {
        public string CardTitle = "";
        public int TitleHeight = 44;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TextRenderer.DrawText(e.Graphics, CardTitle, Theme.H2Font,
                new Rectangle(20, 8, Width - 40, TitleHeight), Theme.Foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    // ================= 图标缓存 =================
    public static class IconCache
    {
        private static readonly Dictionary<string, Image> _cache = new();
        private static readonly object _lock = new object();

        public static Image Placeholder(char letter)
        {
            var bmp = new Bitmap(96, 96);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Theme.Accent)) g.Clear(ControlPaint.Light(Theme.Accent, 0.6f));
                using (var f = new Font("Segoe UI", 34f, FontStyle.Bold))
                {
                    var sz = g.MeasureString(letter.ToString(), f);
                    TextRenderer.DrawText(g, letter.ToString(), f,
                        new Rectangle(0, 0, 96, 96), Theme.IsDark ? Color.White : Color.FromArgb(40, 40, 46),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            return bmp;
        }

        /// <summary>异步加载网络图标，回调可能在后台线程触发（内部会 BeginInvoke 由调用方处理）。</summary>
        public static void Get(string url, string key, Action<Image> done)
        {
            if (string.IsNullOrEmpty(url)) { done(null); return; }
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var img)) { done(img); return; }
            }
            Task.Run(() =>
            {
                try
                {
                    var dir = Path.Combine(Path.GetTempPath(), "easyer-icons");
                    Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, Sanitize(key) + ".img");
                    if (!File.Exists(file))
                    {
                        var bytes = EasyerLauncher.Core.Net.Http.Client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                        File.WriteAllBytes(file, bytes);
                    }
                    using (var fs = File.OpenRead(file))
                    using (var raw = Image.FromStream(fs))
                    {
                        var scaled = new Bitmap(raw, 96, 96);
                        lock (_lock) { _cache[key] = scaled; }
                        done(scaled);
                    }
                }
                catch { done(null); }
            });
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_');
            return sb.ToString();
        }
    }

    // ================= 图形化内容卡片（Modrinth 等）=================
    public class ContentCard : Control
    {
        public string IconUrl = "";
        public string IconKey = "";
        public string CardTitle = "";
        public string Sub = "";
        public string Desc = "";
        public Action OnAction;          // "获取" 按钮回调
        public Action OnOpen;            // 点卡片本体回调

        private bool _hover;
        private Image _icon;
        private Rectangle _actionRect;

        public ContentCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(310, 118);
        }

        public void LoadIcon(Action invalidate)
        {
            if (string.IsNullOrEmpty(IconUrl)) return;
            IconCache.Get(IconUrl, IconKey, img =>
            {
                _icon = img;
                invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var overAction = _actionRect.Contains(e.Location);
            Cursor = overAction ? Cursors.Hand : Cursors.Hand;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_actionRect.Contains(e.Location)) { OnAction?.Invoke(); return; }
            OnOpen?.Invoke();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedPath(rect, 12))
            {
                using (var b = new SolidBrush(_hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                using (var pen = new Pen(_hover ? Theme.Accent : Theme.Border)) g.DrawPath(pen, path);
            }

            // 图标（圆角裁剪）
            var iconRect = new Rectangle(14, (Height - 64) / 2, 64, 64);
            using (var clip = Theme.RoundedPath(iconRect, 10))
            {
                var prev = g.Clip;
                g.SetClip(clip);
                if (_icon != null)
                    g.DrawImage(_icon, iconRect);
                else
                {
                    using (var b = new SolidBrush(Theme.Input)) g.FillPath(b, Theme.RoundedPath(iconRect, 10));
                    TextRenderer.DrawText(g, "?", Theme.BigIconFont, iconRect, Theme.ForegroundDim,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                g.Clip = prev;
            }

            // 标题
            var textLeft = iconRect.Right + 12;
            var titleRect = new Rectangle(textLeft, 14, Width - textLeft - 76, 24);
            TextRenderer.DrawText(g, CardTitle, new Font(Theme.DefaultFontFamily, 10.5f, FontStyle.Bold),
                titleRect, Theme.Foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 副标题
            TextRenderer.DrawText(g, Sub, Theme.SmallFont,
                new Rectangle(textLeft, 40, Width - textLeft - 20, 18),
                Theme.ForegroundDim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 描述（两行）
            TextRenderer.DrawText(g, Desc, Theme.SmallFont,
                new Rectangle(textLeft, 60, Width - textLeft - 20, 40),
                Theme.ForegroundDim, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

            // "获取" 小按钮
            var btn = new Rectangle(Width - 68, 16, 54, 26);
            _actionRect = btn;
            using (var bpath = Theme.RoundedPath(btn, 13))
            using (var b = new SolidBrush(_hover ? Theme.Accent : Theme.Input))
                g.FillPath(b, bpath);
            TextRenderer.DrawText(g, "获取", Theme.SmallFont, btn, _hover ? Theme.AccentText : Theme.Foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // ================= 版本卡片（Minecraft 版本）=================
    public class VersionCard : Control
    {
        public string VersionId = "";
        public string VersionType = "";
        public string TypeCn = "";
        public string DateStr = "";
        public Action OnInstall;

        private bool _hover;

        public VersionCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(228, 84);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            OnInstall?.Invoke();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedPath(rect, 12))
            {
                using (var b = new SolidBrush(_hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                using (var pen = new Pen(_hover ? Theme.Accent : Theme.Border)) g.DrawPath(pen, path);
            }

            // 类型徽章
            var typeColor = Theme.TypeColor(VersionType);
            var chip = new Rectangle(14, 14, TextRenderer.MeasureText(TypeCn, Theme.SmallFont).Width + 18, 22);
            using (var chipPath = Theme.RoundedPath(chip, 11))
            {
                using (var b = new SolidBrush(Color.FromArgb(40, typeColor)))
                    g.FillPath(b, chipPath);
                using (var pen = new Pen(typeColor))
                    g.DrawPath(pen, chipPath);
            }
            TextRenderer.DrawText(g, TypeCn, Theme.SmallFont, chip, typeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 版本号
            TextRenderer.DrawText(g, VersionId, new Font(Theme.DefaultFontFamily, 10.5f, FontStyle.Bold),
                new Rectangle(14, 40, Width - 28, 22), Theme.Foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 日期 + 提示
            TextRenderer.DrawText(g, DateStr + (_hover ? "   ·  点击安装" : ""), Theme.SmallFont,
                new Rectangle(14, 60, Width - 28, 16), Theme.ForegroundDim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
