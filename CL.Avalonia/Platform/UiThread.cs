using Avalonia.Threading;
using System;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    internal static class UiThread
    {
        public static void Run(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }
    }
}
