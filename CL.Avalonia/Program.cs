using Avalonia;
using Velopack;

namespace CL_CLegendary_Launcher_.Avalonia
{
    internal static class Program
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        [System.STAThread]
        public static int Main(string[] args)
        {
            VelopackApp.Build().Run();
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }
}
