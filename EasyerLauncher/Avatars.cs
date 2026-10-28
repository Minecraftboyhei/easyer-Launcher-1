using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using EasyerLauncher.Core.Config;

namespace EasyerLauncher
{
    /// <summary>账号头像：正版/外置走 Crafatar，拉不到就用本地生成的 Steve 头像，绝不空白。</summary>
    public static class Avatars
    {
        private static readonly Dictionary<string, Image> Cache = new();
        private static readonly object Lock = new();
        private static Image _steve;

        public static Image Steve()
        {
            lock (Lock)
            {
                if (_steve == null) _steve = Icons.SteveAvatar(64);
                return _steve;
            }
        }

        private static string KeyOf(AccountInfo acc)
        {
            var uuid = (acc?.Uuid ?? "").Trim();
            return uuid.Length > 0 ? uuid.ToLowerInvariant() : "offline:" + (acc?.Name ?? "").ToLowerInvariant();
        }

        /// <summary>同步取缓存；没缓存就返回 null 并后台拉取。</summary>
        public static Image GetCached(AccountInfo acc)
        {
            var key = KeyOf(acc);
            lock (Lock) if (Cache.TryGetValue(key, out var img)) return img;
            Fetch(acc, key);
            return null;
        }

        private static void Fetch(AccountInfo acc, string key)
        {
            var uuid = (acc?.Uuid ?? "").Trim();
            var offline = string.IsNullOrEmpty(uuid) ||
                          string.Equals(acc.AccountType, "offline", StringComparison.OrdinalIgnoreCase);
            if (offline)
            {
                lock (Lock) Cache[key] = Icons.SteveAvatar(64);
                return;
            }

            Task.Run(() =>
            {
                var url = "https://crafatar.com/avatars/" + uuid + "?size=64&overlay";
                IconCache.Get(url, "avatar-" + uuid, img =>
                {
                    if (img != null)
                    {
                        lock (Lock) Cache[key] = img;
                    }
                    else
                    {
                        // 头像拉不动（国内常见）：用本地 Steve，至少不是空白方块
                        bool has;
                        lock (Lock) has = Cache.ContainsKey(key);
                        if (!has) lock (Lock) Cache[key] = Icons.SteveAvatar(64);
                    }
                });
            });
        }
    }

    /// <summary>头像控件（圆角裁剪，无图时显示 Steve）。</summary>
    public class AvatarBox : Control
    {
        private string _uuid = "";
        private string _name = "";
        private string _accountType = "offline";
        private Image _pending;

        public int CornerRadius = 8;

        public AvatarBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(44, 44);
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
                    await Task.Delay(400);
                    var img = Avatars.GetCached(target);
                    try
                    {
                        if (!self.IsDisposed && self.IsHandleCreated)
                            self.BeginInvoke(new Action(() => { self._pending = img; self.Invalidate(); }));
                    }
                    catch { }
                });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.BackdropOf(this));

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedPath(r, CornerRadius);
            var img = _pending ?? Avatars.Steve();

            var prev = g.Clip;
            g.SetClip(path);
            g.DrawImage(img, r);
            g.Clip = prev;
            using (var pen = new Pen(Theme.Border))
                g.DrawPath(pen, path);
        }
    }
}
