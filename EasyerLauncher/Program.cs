using System;
using System.Windows.Forms;

namespace EasyerLauncher
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            var cfg = EasyerLauncher.Core.Config.LauncherConfig.Load();
            Theme.Apply(cfg.ThemeMode, cfg.AccentColor, cfg.CustomBackground, cfg.CustomCard, cfg.CustomForeground);
            Application.Run(new MainForm());
        }
    }
}
