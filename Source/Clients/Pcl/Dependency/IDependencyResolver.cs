using System;
using System.Collections.Generic;

namespace Exceptionless.Dependency {
    public interface IDependencyResolver : IDisposable {
        object GetService(Type serviceType);
        IEnumerable<object> GetServices(Type serviceType);
        void Register(Type serviceType, Func<object> activator);
        void Register(Type serviceType, IEnumerable<Func<object>> activators);
    }
}
