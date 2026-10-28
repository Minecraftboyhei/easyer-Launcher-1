using System;
using System.Reflection;

namespace EasyerLauncher
{
    /// <summary>版本与项目元信息（版本号统一从程序集读，不再到处写死 "v1.0"）。</summary>
    public static class AppInfo
    {
        public const string RepoOwner = "Minecraftboyhei";
        public const string RepoName = "easyer-Launcher-1";
        public const string RepoUrl = "https://github.com/Minecraftboyhei/easyer-Launcher-1";
        public const string IssuesUrl = "https://github.com/Minecraftboyhei/easyer-Launcher-1/issues";
        public const string ReleasesUrl = "https://github.com/Minecraftboyhei/easyer-Launcher-1/releases";
        public const string AppName = "Easyer Launcher 1";

        private static string _version;

        /// <summary>三段式版本号，如 1.0.7。</summary>
        public static string Version
        {
            get
            {
                if (_version != null) return _version;
                try
                {
                    var v = Assembly.GetExecutingAssembly().GetName().Version;
                    _version = v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
                }
                catch { _version = "1.0.0"; }
                return _version;
            }
        }

        public static string Display => "v" + Version;

        /// <summary>语义化版本比较：newer &gt; current 返回 true。</summary>
        public static bool IsNewer(string current, string newer)
        {
            static int[] Parse(string s)
            {
                var parts = (s ?? "0.0.0").TrimStart('v', 'V').Split('.');
                var nums = new int[3];
                for (int i = 0; i < 3; i++)
                    nums[i] = i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
                return nums;
            }
            var a = Parse(current);
            var b = Parse(newer);
            for (int i = 0; i < 3; i++)
            {
                if (b[i] > a[i]) return true;
                if (b[i] < a[i]) return false;
            }
            return false;
        }
    }
}
