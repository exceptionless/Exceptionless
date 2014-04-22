using System;

namespace Exceptionless.Dependency {
    public interface IDependencyResolver : IDisposable {
        object Resolve(Type serviceType);
        void Register(Type serviceType, Type concreteType);
        void Register(Type serviceType, Func<object> activator);
    }
}
