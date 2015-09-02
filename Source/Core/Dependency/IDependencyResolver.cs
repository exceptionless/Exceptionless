using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Dependency {
    public interface IDependencyResolver {
        object GetService(Type serviceType);
        IEnumerable<object> GetServices(Type serviceType);
    }
}
