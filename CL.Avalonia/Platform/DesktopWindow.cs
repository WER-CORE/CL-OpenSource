using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    internal static class DesktopWindow
    {
        public static Window? Main =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
