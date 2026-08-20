using CL.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace CL_CLegendary_Launcher_.PlatformImpl
{
    public class WpfDispatcherService : IDispatcherService
    {
        public bool CheckAccess()
        {
            return Application.Current.Dispatcher.CheckAccess();
        }

        public void Invoke(Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }

        public Task InvokeAsync(Action action)
        {
            return Application.Current.Dispatcher.InvokeAsync(action).Task;
        }

        public Task<T> InvokeAsync<T>(Func<T> action)
        {
            return Application.Current.Dispatcher.InvokeAsync(action).Task;
        }
    }
}
