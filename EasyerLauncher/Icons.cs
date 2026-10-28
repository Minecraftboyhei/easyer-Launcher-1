using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EasyerLauncher
{
    /// <summary>
    /// 自绘矢量图标库。
    /// 之前整套界面靠 Segoe MDL2 Assets 的字形码位画图标，Win11 上有一半码位
    /// （EA86/E943/E720/E73E…）根本不存在，显示成空白或豆腐块。这里改成纯几何自绘，
    /// 和字体彻底脱钩，任何机器、任何 DPI 长得都一样。
    /// </summary>
    public static class Icons
    {
        private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            "home", "download", "wrench", "puzzle", "package", "user", "settings", "play",
            "refresh", "folder", "shield", "memory", "install", "search", "next", "check",
            "info", "alert", "update", "game", "palette", "close", "clock", "trash", "plus",
            "link", "cube"
        };

        /// <summary>旧的字形码位 → 图标名（历史代码里到处写死的 \uE896 之类，统一兜底）。</summary>
        private static readonly Dictionary<string, string> GlyphAlias = new()
        {
            ["\uE80F"] = "home",      ["\uE896"] = "download", ["\uE90F"] = "wrench",
            ["\uEA86"] = "puzzle",    ["\uE7B8"] = "package",  ["\uE77B"] = "user",
            ["\uE713"] = "settings",  ["\uE768"] = "play",     ["\uE72C"] = "refresh",
            ["\uE8B7"] = "folder",    ["\uE943"] = "shield",   ["\uE950"] = "memory",
            ["\uE73E"] = "install",   ["\uE721"] = "search",   ["\uE72A"] = "next",
            ["\uE73D"] = "check",     ["\uE946"] = "info",     ["\uE7BA"] = "alert",
            ["\uE895"] = "update",    ["\uE7FC"] = "game",     ["\uE790"] = "palette",
            ["\uE8BB"] = "close",
        };

        /// <summary>把图标名或旧字形码位归一成图标名；都不是就原样返回。</summary>
        public static string Resolve(string nameOrGlyph)
        {
            if (string.IsNullOrEmpty(nameOrGlyph)) return "";
            if (GlyphAlias.TryGetValue(nameOrGlyph, out var mapped)) return mapped;
            return nameOrGlyph;
        }

        public static bool Has(string name) =>
            !string.IsNullOrEmpty(name) && Names.Contains(name);

        /// <summary>在给定矩形内画一个图标（等比居中，线宽恒定 2px 视觉）。</summary>
        public static void Draw(Graphics g, string name, Rectangle bounds, Color color)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;
            var key = Resolve(name);
            if (!Has(key)) { DrawFallback(g, key, bounds, color); return; }

            float s = Math.Min(bounds.Width, bounds.Height) / 24f;
            if (s <= 0.01f) return;

            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
            g.ScaleTransform(s, s);
            g.TranslateTransform(-12f, -12f);

            using var pen = new Pen(color, 2f / s)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var brush = new SolidBrush(color);

            switch (key.ToLowerInvariant())
            {
                case "home":
                    g.DrawLines(pen, new[] { new PointF(3, 11), new PointF(12, 4), new PointF(21, 11) });
                    g.DrawLines(pen, new[] { new PointF(6, 11.5f), new PointF(6, 20), new PointF(18, 20), new PointF(18, 11.5f) });
                    g.DrawLine(pen, 10, 20, 10, 15);
                    g.DrawLine(pen, 14, 20, 14, 15);
                    g.DrawLine(pen, 10, 15, 14, 15);
                    break;

                case "download":
                    g.DrawLine(pen, 12, 4, 12, 15);
                    g.DrawLines(pen, new[] { new PointF(7, 11), new PointF(12, 16), new PointF(17, 11) });
                    g.DrawLine(pen, 5, 19, 19, 19);
                    break;

                case "wrench":   // 扳手：加载器
                    g.DrawEllipse(pen, 3.5f, 3.5f, 9, 9);
                    g.DrawLine(pen, 10, 10, 19.5f, 19.5f);
                    g.DrawLine(pen, 16.5f, 19.5f, 19.5f, 19.5f);
                    g.DrawLine(pen, 19.5f, 16.5f, 19.5f, 19.5f);
                    break;

                case "puzzle":   // 拼图：模组
                    g.DrawRectangle(pen, 3, 4, 12, 12);
                    g.DrawRectangle(pen, 15, 7, 6, 6);
                    g.DrawLine(pen, 3, 10, 15, 10);
                    g.DrawLine(pen, 9, 10, 9, 16);
                    break;

                case "package":  // 盒子：整合包
                    g.DrawRectangle(pen, 3, 7, 18, 14);
                    g.DrawLine(pen, 3, 11, 21, 11);
                    g.DrawLine(pen, 12, 7, 12, 21);
                    break;

                case "user":
                    g.DrawEllipse(pen, 8.5f, 3.5f, 7, 7);
                    g.DrawArc(pen, 4, 10, 16, 14, 180, 180);
                    break;

                case "settings": // 齿轮
                    g.DrawEllipse(pen, 7, 7, 10, 10);
                    g.DrawEllipse(pen, 10, 10, 4, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        float cx = 12f, cy = 12f;
                        g.DrawLine(pen,
                            cx + (float)(Math.Cos(a) * 6.6f), cy + (float)(Math.Sin(a) * 6.6f),
                            cx + (float)(Math.Cos(a) * 10f), cy + (float)(Math.Sin(a) * 10f));
                    }
                    break;

                case "play":
                    g.FillPolygon(brush, new[] { new PointF(8, 5), new PointF(19, 12), new PointF(8, 19) });
                    break;

                case "refresh":
                case "update":
                    g.DrawArc(pen, 3.5f, 3.5f, 17, 17, 25, 150);
                    g.DrawArc(pen, 3.5f, 3.5f, 17, 17, 205, 150);
                    g.FillPolygon(brush, new[] { new PointF(20.5f, 10), new PointF(16.5f, 11.5f), new PointF(19, 15) });
                    g.FillPolygon(brush, new[] { new PointF(3.5f, 14), new PointF(7.5f, 12.5f), new PointF(5, 9) });
                    break;

                case "folder":
                    g.DrawPolygon(pen, new[]
                    {
                        new PointF(3, 6), new PointF(9, 6), new PointF(11.5f, 8.5f),
                        new PointF(21, 8.5f), new PointF(21, 19), new PointF(3, 19)
                    });
                    break;

                case "shield":   // Java
                    using (var sp = new GraphicsPath())
                    {
                        sp.AddLine(12, 3, 20, 6.5f);
                        sp.AddLine(20, 6.5f, 20, 12);
                        sp.AddBezier(20, 12, 20, 17.5f, 16.5f, 19.8f, 12, 21);
                        sp.AddBezier(12, 21, 7.5f, 19.8f, 4, 17.5f, 4, 12);
                        sp.AddLine(4, 12, 4, 6.5f);
                        sp.CloseFigure();
                        g.DrawPath(pen, sp);
                    }
                    g.DrawLines(pen, new[] { new PointF(8.5f, 12), new PointF(11, 14.5f), new PointF(15.5f, 9.5f) });
                    break;

                case "memory":
                    g.DrawRectangle(pen, 3, 8, 18, 8);
                    g.DrawLine(pen, 9, 8, 9, 16);
                    g.DrawLine(pen, 15, 8, 15, 16);
                    for (int i = 0; i < 4; i++)
                        g.DrawLine(pen, 6 + i * 4, 16, 6 + i * 4, 19);
                    break;

                case "install":
                    g.DrawRectangle(pen, 3, 13, 18, 8);
                    g.DrawLine(pen, 3, 17, 21, 17);
                    g.DrawLine(pen, 12, 3, 12, 13);
                    g.DrawLines(pen, new[] { new PointF(8.5f, 9.5f), new PointF(12, 13), new PointF(15.5f, 9.5f) });
                    break;

                case "search":
                    g.DrawEllipse(pen, 4, 4, 12, 12);
                    g.DrawLine(pen, 12.5f, 12.5f, 20, 20);
                    break;

                case "next":
                    g.DrawLines(pen, new[] { new PointF(9, 5), new PointF(16, 12), new PointF(9, 19) });
                    break;

                case "check":
                    g.DrawLines(pen, new[] { new PointF(4.5f, 12.5f), new PointF(10, 18), new PointF(19.5f, 6) });
                    break;

                case "info":
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    g.DrawLine(pen, 12, 11, 12, 17.5f);
                    g.FillEllipse(brush, 11, 6.5f, 2, 2.5f);
                    break;

                case "alert":
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    g.DrawLine(pen, 12, 7, 12, 14);
                    g.FillEllipse(brush, 11, 16.5f, 2, 2);
                    break;

                case "game":     // 手柄
                    using (var gp = new GraphicsPath())
                    {
                        gp.AddArc(2, 8, 6, 6, 180, 90);
                        gp.AddArc(16, 8, 6, 6, 270, 90);
                        gp.AddArc(16, 11, 6, 6, 0, 90);
                        gp.AddArc(2, 11, 6, 6, 90, 90);
                        gp.CloseFigure();
                        g.DrawPath(pen, gp);
                    }
                    g.DrawLine(pen, 6.5f, 11, 9.5f, 11);
                    g.DrawLine(pen, 8, 9.5f, 8, 12.5f);
                    g.FillEllipse(brush, 14.5f, 10.5f, 2, 2);
                    g.FillEllipse(brush, 17.5f, 9.5f, 2, 2);
                    break;

                case "palette":
                    g.DrawEllipse(pen, 3.5f, 4, 17, 16);
                    g.FillEllipse(brush, 7, 8, 2.4f, 2.4f);
                    g.FillEllipse(brush, 11, 7, 2.4f, 2.4f);
                    g.FillEllipse(brush, 15, 9.5f, 2.4f, 2.4f);
                    break;

                case "close":
                    g.DrawLine(pen, 6, 6, 18, 18);
                    g.DrawLine(pen, 18, 6, 6, 18);
                    break;

                case "clock":
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    g.DrawLine(pen, 12, 7, 12, 12);
                    g.DrawLine(pen, 12, 12, 16, 14);
                    break;

                case "trash":
                    g.DrawLine(pen, 4, 7, 20, 7);
                    g.DrawLines(pen, new[] { new PointF(6, 7), new PointF(7, 20), new PointF(17, 20), new PointF(18, 7) });
                    g.DrawLine(pen, 10, 10, 10, 17);
                    g.DrawLine(pen, 14, 10, 14, 17);
                    break;

                case "plus":
                    g.DrawLine(pen, 12, 5, 12, 19);
                    g.DrawLine(pen, 5, 12, 19, 12);
                    break;

                case "link":
                    using (var lp = new GraphicsPath())
                    {
                        lp.AddArc(3, 3, 12, 12, 135, 270);
                        g.DrawPath(pen, lp);
                    }
                    g.DrawLine(pen, 10, 14, 14, 10);
                    break;

                case "cube":    // 线框方块
                    g.DrawRectangle(pen, 4, 6, 16, 13);
                    g.DrawLine(pen, 4, 6, 9, 2.5f);
                    g.DrawLine(pen, 20, 6, 15, 2.5f);
                    g.DrawLine(pen, 9, 2.5f, 15, 2.5f);
                    g.DrawLine(pen, 15, 2.5f, 15, 8.5f);
                    g.DrawLine(pen, 9, 2.5f, 9, 8.5f);
                    break;
            }

            g.Restore(st);
        }

        /// <summary>未知图标名：单个字符画字符，多个字符画首字母圆牌，绝不画豆腐块。</summary>
        private static void DrawFallback(Graphics g, string key, Rectangle bounds, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (string.IsNullOrEmpty(key)) return;
            if (key.Length == 1 && char.IsLetterOrDigit(key[0]))
            {
                using var f = new Font("Segoe UI", Math.Max(8f, Math.Min(bounds.Width, bounds.Height) * 0.55f), FontStyle.Bold);
                TextRenderer.DrawText(g, key.ToUpperInvariant(), f, bounds, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            var r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (var path = Theme.RoundedPath(r, Math.Min(6, r.Height / 3)))
            using (var b = new SolidBrush(Color.FromArgb(38, color)))
                g.FillPath(b, path);
            using var bf = new Font("Segoe UI", Math.Max(7f, bounds.Height * 0.42f), FontStyle.Bold);
            TextRenderer.DrawText(g, key.Substring(0, 1).ToUpperInvariant(), bf, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ================= 加载器 / 版本图标 =================

        /// <summary>加载器或版本类型的彩色徽标（字母牌，小尺寸也认得出）。</summary>
        public static void DrawKind(Graphics g, string kind, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            var k = (kind ?? "").ToLowerInvariant();

            if (k is "vanilla" or "mc" or "standard")
            {
                DrawGrassBlock(g, bounds);
                return;
            }
            if (k is "pack" or "modpack")
            {
                Draw(g, "package", bounds, Color.FromArgb(167, 139, 250));
                return;
            }

            Color bg; string letter; Color fg = Color.White;
            switch (k)
            {
                case "fabric": bg = Color.FromArgb(219, 181, 151); letter = "F"; fg = Color.FromArgb(60, 40, 25); break;
                case "quilt": bg = Color.FromArgb(178, 139, 224); letter = "Q"; break;
                case "forge": bg = Color.FromArgb(139, 94, 60); letter = "F"; break;
                case "neoforge": bg = Color.FromArgb(241, 100, 54); letter = "N"; break;
                default: bg = Color.FromArgb(90, 100, 120); letter = string.IsNullOrEmpty(kind) ? "?" : kind.Substring(0, 1).ToUpperInvariant(); break;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (var path = Theme.RoundedPath(r, Math.Max(3, r.Height / 4)))
            using (var b = new SolidBrush(bg))
                g.FillPath(b, path);
            using var f = new Font("Segoe UI", Math.Max(7f, bounds.Height * 0.52f), FontStyle.Bold);
            TextRenderer.DrawText(g, letter, f, bounds, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>草方块（原版 Minecraft 的等距立方体）。</summary>
        public static void DrawGrassBlock(Graphics g, Rectangle bounds)
        {
            int w = bounds.Width, h = bounds.Height;
            var cx = bounds.X + w / 2f;
            var top = bounds.Y;
            var mid = bounds.Y + h * 0.32f;
            var bottom = bounds.Y + h;
            var half = w / 2f;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var topB = new SolidBrush(Color.FromArgb(122, 192, 90)))
                g.FillPolygon(topB, new[]
                {
                    new PointF(cx, top), new PointF(bounds.Right, mid),
                    new PointF(cx, mid + h * 0.32f), new PointF(bounds.Left, mid)
                });
            using (var leftB = new SolidBrush(Color.FromArgb(122, 84, 50)))
                g.FillPolygon(leftB, new[]
                {
                    new PointF(bounds.Left, mid), new PointF(cx, mid + h * 0.32f),
                    new PointF(cx, bottom), new PointF(bounds.Left, bottom - h * 0.32f)
                });
            using (var rightB = new SolidBrush(Color.FromArgb(94, 64, 38)))
                g.FillPolygon(rightB, new[]
                {
                    new PointF(bounds.Right, mid), new PointF(cx, mid + h * 0.32f),
                    new PointF(cx, bottom), new PointF(bounds.Right, bottom - h * 0.32f)
                });
        }

        /// <summary>离线账号的 Steve 风头像（本地生成，不联网）。</summary>
        public static Bitmap SteveAvatar(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                float u = size / 8f;
                void Cell(int x, int y, Color c)
                {
                    using var b = new SolidBrush(c);
                    g.FillRectangle(b, x * u, y * u, u + 0.6f, u + 0.6f);
                }
                var hair = Color.FromArgb(60, 40, 24);
                var skin = Color.FromArgb(198, 138, 96);
                var eye = Color.FromArgb(70, 70, 90);
                var mouth = Color.FromArgb(120, 70, 50);
                for (int x = 0; x < 8; x++) { Cell(x, 0, hair); Cell(x, 1, hair); }
                for (int x = 0; x < 8; x++) Cell(x, 2, x is 0 or 7 ? hair : skin);
                Cell(1, 3, skin); Cell(2, 3, eye); Cell(3, 3, skin);
                Cell(4, 3, skin); Cell(5, 3, eye); Cell(6, 3, skin);
                for (int x = 1; x < 7; x++) Cell(x, 4, skin);
                Cell(2, 5, skin); Cell(3, 5, mouth); Cell(4, 5, mouth); Cell(5, 5, skin);
                for (int x = 1; x < 7; x++) Cell(x, 6, skin);
                for (int x = 1; x < 7; x++) Cell(x, 7, x is 1 or 6 ? skin : Color.FromArgb(150, 100, 66));
            }
            return bmp;
        }

        /// <summary>把任意图裁成圆角（头像用）。</summary>
        public static Bitmap RoundClip(Image src, int size, int radius)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using var path = Theme.RoundedPath(new Rectangle(0, 0, size - 1, size - 1), radius);
                g.SetClip(path);
                g.DrawImage(src, 0, 0, size, size);
            }
            return bmp;
        }
    }

    /// <summary>一个图标的占位控件（替代以前用 Segoe 字形当文本的 Label）。</summary>
    public class IconBox : Control
    {
        private string _name = "";

        public string IconName
        {
            get => _name;
            set { _name = value ?? ""; Invalidate(); }
        }

        public Color IconColor = Color.Empty;   // Empty = 跟随主题强调色

        public IconBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(28, 28);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));
            var color = IconColor == Color.Empty ? Theme.Accent : IconColor;
            Icons.Draw(g, _name, new Rectangle(0, 0, Width, Height), color);
        }
    }

    /// <summary>加载器 / 版本类型徽标控件。</summary>
    public class KindBadge : Control
    {
        public string Kind = "vanilla";

        public KindBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(32, 32);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackdropOf(this));
            Icons.DrawKind(g, Kind, new Rectangle(0, 0, Width, Height));
        }
    }
}
