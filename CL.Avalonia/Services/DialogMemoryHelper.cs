using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CL.Core.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public static class DialogMemoryHelper
{
    public static void EnableAutoMemoryCleanup(this Window window)
    {
        window.Closed += (sender, e) =>
        {
            if (window.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
            window.DataContext = null;
            window.Content = null;

            _ = Task.Run(async () =>
            {
                await Task.Delay(250);
                await MemoryCleaner.FlushMemoryAsync(trimWorkingSet: OperatingSystem.IsWindows() || OperatingSystem.IsLinux());
            });
        };
    }
}
