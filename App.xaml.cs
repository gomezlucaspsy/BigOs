using System.Windows;
using UnixBrowser.Config;

namespace UnixBrowser
{
    public partial class App : Application
    {
        public static bool IsBeastMode { get; private set; } = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Check for --beast command-line argument
            if (e.Args.Length > 0 && e.Args.Contains("--beast"))
            {
                IsBeastMode = true;
            }

            base.OnStartup(e);
        }
    }
}

