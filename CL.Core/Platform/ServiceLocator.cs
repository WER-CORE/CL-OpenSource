using System;
using System.Collections.Generic;

namespace CL.Core.Platform
{
    public class ServiceLocator
    {
        private static readonly ServiceLocator _current = new ServiceLocator();
        public static ServiceLocator Current => _current;

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        private ServiceLocator() { }

        public void Register<TInterface, TImplementation>(TImplementation implementation) 
            where TImplementation : TInterface
        {
            _services[typeof(TInterface)] = implementation;
        }

        public T GetService<T>()
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }
    }
}
