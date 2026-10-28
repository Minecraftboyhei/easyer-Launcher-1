using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyerLauncher
{
    /// <summary>
    /// 自动更新：GitHub Releases 检查（多镜像兜底，国内也能通）→ 下载 → 关进程自替换 → 重启。
    /// 单文件 exe 没法热替换，所以走一个 cmd 脚本：等旧进程退出后 copy 覆盖再拉起来。
    /// </summary>
    public static class Updater
    {
        public class ReleaseInfo
        {
            public string Version = "";
            public string Name = "";
            public string Notes = "";
            public string Url = "";      // 下载直链
            public long Size = 0;
        }

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>GitHub 访问镜像（按顺序试，第一个通就用）。</summary>
        private static readonly string[] Mirrors =
        {
            "",                            // 直连
            "https://gh-proxy.com/",
            "https://ghproxy.net/",
            "https://gh-proxy.org/",
        };

        static Updater()
        {
            try { Http.DefaultRequestHeaders.UserAgent.ParseAdd("EasyerLauncher/" + AppInfo.Version); } catch { }
        }

        private static string ApiUrl(string mirror) =>
            mirror + "https://api.github.com/repos/" + AppInfo.RepoOwner + "/" + AppInfo.RepoName + "/releases/latest";

        /// <summary>检查最新版；无更新或全失败返回 null。</summary>
        public static async Task<ReleaseInfo> CheckAsync()
        {
            foreach (var m in Mirrors)
            {
                try
                {
                    var json = await Http.GetStringAsync(ApiUrl(m));
                    var rel = Parse(json);
                    if (rel == null) continue;
                    if (!AppInfo.IsNewer(AppInfo.Version, rel.Version)) return null;
                    return rel;
                }
                catch { }
            }
            return null;
        }

        private static ReleaseInfo Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var info = new ReleaseInfo();

            if (root.TryGetProperty("tag_name", out var tag))
                info.Version = (tag.GetString() ?? "").Trim().TrimStart('v', 'V');
            if (string.IsNullOrEmpty(info.Version)) return null;

            if (root.TryGetProperty("name", out var nm)) info.Name = nm.GetString() ?? "";
            if (root.TryGetProperty("body", out var body)) info.Notes = body.GetString() ?? "";

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (a.TryGetProperty("browser_download_url", out var u))
                        info.Url = u.GetString() ?? "";
                    if (a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz))
                        info.Size = sz;
                    break;
                }
            }
            return string.IsNullOrEmpty(info.Url) ? null : info;
        }

        /// <summary>下载到临时目录，progress 回调 0~1。</summary>
        public static async Task<string> DownloadAsync(ReleaseInfo rel, Action<float> progress)
        {
            if (rel == null || string.IsNullOrEmpty(rel.Url)) throw new Exception("没有可下载的安装包");

            var dir = Path.Combine(Path.GetTempPath(), "easyer-update");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, "EasyerLauncher-" + rel.Version + ".exe");

            Exception last = null;
            foreach (var m in Mirrors)
            {
                try
                {
                    var url = m + rel.Url;
                    using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();
                    var total = resp.Content.Headers.ContentLength ?? rel.Size;
                    using var src = await resp.Content.ReadAsStreamAsync();
                    using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                    var buf = new byte[65536];
                    long read = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await fs.WriteAsync(buf, 0, n);
                        read += n;
                        if (total > 0) progress?.Invoke(Math.Min(1f, read / (float)total));
                    }
                    progress?.Invoke(1f);
                    return dest;
                }
                catch (Exception ex) { last = ex; }
            }
            throw last ?? new Exception("下载失败");
        }

        /// <summary>写自替换脚本并重启（等旧进程退出后覆盖，避免文件占用）。</summary>
        public static void ApplyAndRestart(string newExe)
        {
            try
            {
                var self = Application.ExecutablePath;
                if (string.IsNullOrEmpty(self) || !File.Exists(self)) return;

                var script = Path.Combine(Path.GetTempPath(), "easyer-update.cmd");
                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("chcp 936 >nul");
                sb.AppendLine(":wait");
                sb.AppendLine($"tasklist /FI \"PID eq {Environment.ProcessId}\" 2>nul | find \"{Environment.ProcessId}\" >nul");
                sb.AppendLine("if errorlevel 1 goto replace");
                sb.AppendLine("ping -n 2 127.0.0.1 >nul");
                sb.AppendLine("goto wait");
                sb.AppendLine(":replace");
                sb.AppendLine($"copy /Y \"{newExe}\" \"{self}\"");
                sb.AppendLine($"start \"\" \"{self}\"");
                sb.AppendLine("del \"%~f0\"");
                File.WriteAllText(script, sb.ToString(), Encoding.Default);

                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
            Application.Exit();
        }
    }

    /// <summary>更新提示对话框。</summary>
    public class UpdateDialog : Form
    {
        private readonly Updater.ReleaseInfo _rel;
        private readonly W11Button _okBtn;
        private readonly ProgressBar _bar;
        private readonly Label _state;
        private string _downloaded;

        public UpdateDialog(Updater.ReleaseInfo rel)
        {
            _rel = rel;
            Text = "发现新版本";
            Size = new Size(560, 420);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            var head = new Label
            {
                Text = "Easyer Launcher " + AppInfo.Display + "  →  v" + rel.Version,
                Font = new Font(Theme.DefaultFontFamily, 13f, FontStyle.Bold),
                Bounds = new Rectangle(24, 20, 500, 30)
            };
            Controls.Add(head);

            var sizeTip = rel.Size > 0
                ? "   更新包大小约 " + (rel.Size / 1024.0 / 1024.0).ToString("0.0") + " MB"
                : "";
            var sub = new Label
            {
                Text = "更新内容（GitHub Release）" + sizeTip,
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                Bounds = new Rectangle(24, 54, 500, 20)
            };
            Controls.Add(sub);

            var notes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.SmallFont,
                Bounds = new Rectangle(24, 80, 496, 200),
                Text = string.IsNullOrWhiteSpace(rel.Notes) ? "（这次没写更新说明）" : rel.Notes
            };
            Controls.Add(notes);

            _state = new Label
            {
                Text = "更新时会关闭启动器并自动替换，游戏目录和账号不受影响。",
                ForeColor = Theme.ForegroundDim,
                Font = Theme.SmallFont,
                Bounds = new Rectangle(24, 290, 496, 20)
            };
            Controls.Add(_state);

            _bar = new ProgressBar
            {
                Bounds = new Rectangle(24, 314, 496, 12),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            Controls.Add(_bar);

            _okBtn = new W11Button
            {
                Text = "立即更新",
                IconGlyph = "download",
                ButtonKind = W11Button.Kind.Accent,
                Font = new Font(Theme.DefaultFontFamily, 11f, FontStyle.Bold),
                Bounds = new Rectangle(24, 340, 200, 44)
            };
            _okBtn.Click += (s, e) =>
            {
                if (_downloaded != null) { Updater.ApplyAndRestart(_downloaded); return; }
                _ = DoUpdate();
            };
            Controls.Add(_okBtn);

            var later = new W11Button
            {
                Text = "以后再说",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(240, 342, 120, 40)
            };
            later.Click += (s, e) => Close();
            Controls.Add(later);

            var manual = new W11Button
            {
                Text = "手动下载",
                IconGlyph = "link",
                ButtonKind = W11Button.Kind.Ghost,
                Bounds = new Rectangle(376, 342, 144, 40)
            };
            manual.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(AppInfo.ReleasesUrl) { UseShellExecute = true });
                }
                catch { }
            };
            Controls.Add(manual);

            Theme.ApplyTo(this);
            Theme.EnableWin11Window(this);
        }

        private async Task DoUpdate()
        {
            _okBtn.Enabled = false;
            _bar.Visible = true;
            _bar.Value = 0;
            _state.Text = "正在下载更新包 ...";
            try
            {
                _downloaded = await Updater.DownloadAsync(_rel, p =>
                {
                    BeginInvoke(new Action(() =>
                    {
                        _bar.Value = (int)Math.Round(p * 100);
                        _state.Text = "正在下载更新包 ... " + (int)Math.Round(p * 100) + "%";
                    }));
                });
                _state.Text = "下载完成，点「重启并更新」应用新版本。";
                _okBtn.Text = "重启并更新";
                _okBtn.IconGlyph = "refresh";
                _okBtn.ButtonKind = W11Button.Kind.Accent;
                _okBtn.Enabled = true;
                _okBtn.Invalidate();
            }
            catch (Exception ex)
            {
                _state.Text = "下载失败：" + ex.Message + "（可以点「手动下载」去 GitHub 拿）";
                _okBtn.Enabled = true;
                _okBtn.Invalidate();
            }
        }

    }
}
