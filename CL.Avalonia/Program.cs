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
                    MaxGpuResourceSizeBytes = 64 * 1024 * 1024
                })
                .LogToTrace();

        [System.STAThread]
        public static int Main(string[] args)
        {
            if (System.OperatingSystem.IsLinux())
            {
                try
                {
                    if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MALLOC_ARENA_MAX")))
                    {
                        System.Environment.SetEnvironmentVariable("MALLOC_ARENA_MAX", "2");
                    }
                    if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MALLOC_TRIM_THRESHOLD_")))
                    {
                        System.Environment.SetEnvironmentVariable("MALLOC_TRIM_THRESHOLD_", "131072");
                    }
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
