using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CL.Core.Interfaces;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public class AvaloniaDispatcherService : IDispatcherService
{
    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    public async Task InvokeAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    public async Task<T> InvokeAsync<T>(Func<T> action)
    {
        return await Dispatcher.UIThread.InvokeAsync(action);
    }
}
