using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using EasyerLauncher.Core.Config;

namespace EasyerLauncher
{
    /// <summary>
    /// 账号头像。统一归一到 8x8 原图（Minecraft 头像本质就是 8x8 像素脸），
    /// 绘制时最近邻整数倍放大，得到 PCL 那种硬边像素方块；拉不到就用本地 Steve。
    /// </summary>
    public static class Avatars
    {
        private static readonly Dictionary<string, Image> Cache = new();
        private static readonly HashSet<string> Pending = new();
        private static readonly object Lock = new();
        private static Image _steve;

        /// <summary>头像源：Minotar 国内可用（Crafatar 会返 500，mc-heads 被 Cloudflare 403）。</summary>
        private static readonly string[] Sources =
        {
            "https://minotar.net/helm/{0}/8",
            "https://crafatar.com/avatars/{0}?size=64&overlay",
        };

        /// <summary>本地 Steve：8x8 原图。</summary>
        public static Image Steve()
        {
            lock (Lock)
            {
                if (_steve == null) _steve = Icons.SteveAvatar(8);
                return _steve;
            }
        }

        private static string KeyOf(AccountInfo acc)
        {
            var uuid = (acc?.Uuid ?? "").Trim();
            return uuid.Length > 0 ? uuid.ToLowerInvariant() : "offline:" + (acc?.Name ?? "").ToLowerInvariant();
        }

        /// <summary>同步取缓存；没缓存就后台拉取并返回 null。</summary>
        public static Image GetCached(AccountInfo acc)
        {
            var key = KeyOf(acc);
            lock (Lock) if (Cache.TryGetValue(key, out var img)) return img;
            Fetch(acc, key);
            return null;
        }

        private static void Fetch(AccountInfo acc, string key)
        {
            lock (Lock) if (!Pending.Add(key)) return;   // 避免轮询时重复下载

            var uuid = (acc?.Uuid ?? "").Trim();
            var offline = string.IsNullOrEmpty(uuid) ||
                          string.Equals(acc.AccountType, "offline", StringComparison.OrdinalIgnoreCase);
            if (offline)
            {
                lock (Lock) { Cache[key] = Steve(); Pending.Remove(key); }
                return;
            }

            Task.Run(() =>
            {
                var img = LoadFromDisk(uuid) ?? Download(uuid);
                lock (Lock)
                {
                    Cache[key] = img ?? Steve();
                    Pending.Remove(key);
                }
            });
        }

        private static Image Download(string uuid)
        {
            foreach (var tpl in Sources)
            {
                try
                {
                    var url = string.Format(tpl, uuid);
                    var bytes = Core.Net.Http.ApiClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    if (bytes == null || bytes.Length < 64) continue;
                    using var ms = new MemoryStream(bytes);
                    using var raw = Image.FromStream(ms);
                    var img = To8(raw);
                    SaveToDisk(uuid, img);
                    return img;
                }
                catch { }
            }
            return null;
        }

        /// <summary>归一到 8x8。源图已是 8x8 就直接拷贝。</summary>
        private static Image To8(Image src)
        {
            if (src.Width == 8 && src.Height == 8) return new Bitmap(src);
            var b = new Bitmap(8, 8);
            using (var g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, 8, 8));
            }
            return b;
        }

        private static string CacheDir => Path.Combine(Path.GetTempPath(), "easyer-avatars");

        private static string FileOf(string uuid) => Path.Combine(CacheDir, uuid + ".png");

        private static Image LoadFromDisk(string uuid)
        {
            try
            {
                var f = FileOf(uuid);
                if (!File.Exists(f)) return null;
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read);
                using var tmp = Image.FromStream(fs);
                return new Bitmap(tmp);
            }
            catch { return null; }
        }

        private static void SaveToDisk(string uuid, Image img)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                img.Save(FileOf(uuid), System.Drawing.Imaging.ImageFormat.Png);
            }
            catch { }
        }
    }

    /// <summary>头像控件。默认 PCL 风格：正方形、不裁圆、不描边、最近邻缩放（像素硬边）。</summary>
    public class AvatarBox : Control
    {
        private string _uuid = "";
        private string _name = "";
        private string _accountType = "offline";
        private Image _pending;

        /// <summary>0 = 方形（默认，PCL 风格）；&gt;0 才做圆角裁剪。</summary>
        public int CornerRadius = 0;
        /// <summary>是否描边。默认不描边。</summary>
        public bool DrawBorder = false;

        public AvatarBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(72, 72);
        }

        /// <summary>绑定账号（传 null 画空头像框）。</summary>
        public void Bind(Core.Config.AccountInfo acc)
        {
            _uuid = acc?.Uuid ?? "";
            _name = acc?.Name ?? "";
            _accountType = acc?.AccountType ?? "offline";
            _pending = acc == null ? null : Avatars.GetCached(acc);
            Invalidate();

            if (acc != null && _pending == null)
            {
                var self = this;
                var target = acc;
                Task.Run(async () =>
                {
                    // 网络头像要等下载，多探几次（300ms / 1s / 1.7s / ...）
                    for (int i = 0; i < 5; i++)
                    {
                        await Task.Delay(i == 0 ? 300 : 700);
                        var img = Avatars.GetCached(target);
                        if (img == null) continue;
                        try
                        {
                            if (!self.IsDisposed && self.IsHandleCreated)
                                self.BeginInvoke(new Action(() => { self._pending = img; self.Invalidate(); }));
                        }
                        catch { }
                        break;
                    }
                });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackdropOf(this));

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            var img = _pending ?? Avatars.Steve();

            // 像素头像必须最近邻：默认插值会把 8x8 的面部糊成一团马赛克
            var prevMode = g.InterpolationMode;
            var prevPix = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            if (CornerRadius > 0)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = Theme.RoundedPath(r, CornerRadius);
                var prev = g.Clip;
                g.SetClip(path);
                g.DrawImage(img, r);
                g.Clip = prev;
                if (DrawBorder)
                    using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
            }
            else
            {
                g.DrawImage(img, r);
                if (DrawBorder)
                    using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, r);
            }

            g.InterpolationMode = prevMode;
            g.PixelOffsetMode = prevPix;
        }
    }
}
