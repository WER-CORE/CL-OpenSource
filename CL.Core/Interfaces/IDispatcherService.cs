using System;
using System.Threading.Tasks;

namespace CL.Core.Interfaces
{
    public interface IDispatcherService
    {
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task<T> InvokeAsync<T>(Func<T> action);
        bool CheckAccess();
    }
}
