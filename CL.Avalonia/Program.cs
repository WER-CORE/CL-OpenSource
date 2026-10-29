using Avalonia;
using Avalonia.Skia;
using Velopack;

namespace CL_CLegendary_Launcher_.Avalonia
{
    internal static class Program
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .With(new SkiaOptions
                {
                    MaxGpuResourceSizeBytes = 32 * 1024 * 1024
                })
                .LogToTrace();

        [System.STAThread]
        public static int Main(string[] args)
        {
            if (System.OperatingSystem.IsLinux())
            {
                try
                {
                    CL.Core.Services.MemoryCleaner.ConfigureLinuxAllocator();
                }
                catch
                {
                }
            }

            VelopackApp.Build().Run();
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }
}
