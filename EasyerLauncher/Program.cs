using System;
using System.Windows.Forms;
using EasyerLauncher.Pages;

namespace EasyerLauncher
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            var cfg = EasyerLauncher.Core.Config.LauncherConfig.Load();
            Theme.Apply(cfg.ThemeMode, cfg.AccentColor, cfg.CustomBackground, cfg.CustomCard, cfg.CustomForeground);

            if (args.Length > 0 && args[0] == "--testpack" && args.Length > 1)
            {
                // 无头测试：下载 .mrpack 并解析清单（验证整合包核心解析逻辑）
                try
                {
                    var url = args[1];
                    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "easyer-test.mrpack");
                    EasyerLauncher.Core.Net.Http.DownloadFile(url, tmp, null, null, 300);
                    var idx = EasyerLauncher.Core.Minecraft.MrpackInstaller.ReadIndex(tmp);
                    Console.WriteLine("PACK OK: name=" + idx.Name +
                        " mc=" + idx.GameVersion +
                        " loader=" + (idx.Loader == "" ? "vanilla" : idx.Loader + "@" + idx.LoaderVersion) +
                        " files=" + idx.Files.Count);
                    System.IO.File.Delete(tmp);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("PACK FAIL: " + ex.Message);
                    Environment.Exit(1);
                }
            }

            if (args.Length > 0 && args[0] == "--testlaunch" && args.Length > 1)
            {
                // 无头诊断：构造真实启动参数并运行 java，把 stdout/stderr 原样打出来
                var cfg2 = EasyerLauncher.Core.Config.LauncherConfig.Load();
                var acc2 = cfg2.Accounts.Find(a =>
                    string.Equals(a.Name, cfg2.ActiveAccountName, StringComparison.OrdinalIgnoreCase))
                    ?? (cfg2.Accounts.Count > 0 ? cfg2.Accounts[0] : null);
                try
                {
                    var opt = new EasyerLauncher.Core.Minecraft.GameLauncher.LaunchOptions
                    {
                        VersionId = args[1],
                        GameDir = cfg2.GameDirectory,
                        RootDir = cfg2.GameDirectory,
                        JavaPath = cfg2.JavaPath,
                        MaxMemoryMB = cfg2.MaxMemoryMB,
                        MinMemoryMB = 512,
                        Account = acc2 == null ? null : new EasyerLauncher.Core.Config.AccountInfo
                        {
                            Name = acc2.Name,
                            Uuid = acc2.Uuid,
                            AccessToken = acc2.AccessToken,
                            AccountType = acc2.AccountType
                        },
                        WindowWidth = cfg2.WindowWidth,
                        WindowHeight = cfg2.WindowHeight,
                        FullScreen = cfg2.FullScreen
                    };
                    var cmdArgs = EasyerLauncher.Core.Minecraft.GameLauncher.BuildArguments(opt);
                    Console.WriteLine("=== ARGS (" + cmdArgs.Length + " chars) ===");
                    Console.WriteLine(cmdArgs.Length > 400 ? cmdArgs.Substring(0, 400) + " ..." : cmdArgs);
                    Console.WriteLine("=== LAUNCH() ===");

                    var proc = EasyerLauncher.Core.Minecraft.GameLauncher.Launch(opt);
                    Console.WriteLine("LAUNCHED pid=" + proc.Id);
                    System.Threading.Thread.Sleep(4000);
                    if (proc.HasExited)
                    {
                        Console.WriteLine("EXITED early, code=" + proc.ExitCode);
                        Environment.Exit(2);
                    }
                    Console.WriteLine("STILL RUNNING — OK");
                    try { proc.Kill(); } catch { }
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("LAUNCH EXCEPTION: " + ex.GetType().Name + " — " + ex.Message);
                    Environment.Exit(1);
                }
            }

            if (args.Length > 0 && args[0] == "--selftest")
            {
                // 无头自检：构造全部弹窗（原崩溃点：Theme.ApplyTo 碰到不支持透明的控件）
                try
                {
                    using var main = new MainForm();
                    using (new InstallPickForm(main, "1.21.1"))
                    using (new LoaderVersionPickForm(main, "1.21.1", "fabric",
                        new System.Collections.Generic.List<string> { "0.16.9", "0.15.11" }))
                    { }
                    Console.WriteLine("SELFTEST OK");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SELFTEST FAIL: " + ex.Message);
                    Environment.Exit(1);
                }
            }

            Application.Run(new MainForm());
        }
    }
}
