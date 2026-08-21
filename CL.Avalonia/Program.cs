using Avalonia;

namespace CL_CLegendary_Launcher_.Avalonia
{
    internal static class Program
    {
        // AppId має збігатися з іменем .desktop-файлу, інакше стільниця
        // не зіставить вікно з ярликом і покаже його без іконки та назви.
        public const string AppId = "cl-launcher";

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions { WmClass = AppId })
                .WithInterFont()
                .LogToTrace();

        [System.STAThread]
        public static int Main(string[] args)
            => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
