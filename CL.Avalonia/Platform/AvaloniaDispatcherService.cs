using Avalonia.Threading;
using CL.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Platform
{
    public class AvaloniaDispatcherService : IDispatcherService
    {
        public void Invoke(Action action) => Dispatcher.UIThread.Post(action);
        public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
        public Task<T> InvokeAsync<T>(Func<T> action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
        public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
    }
}
