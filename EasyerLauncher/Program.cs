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
