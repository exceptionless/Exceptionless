using System;
using TinyIoC;

namespace Exceptionless.Dependency {
    public sealed class DefaultDependencyResolver : IDependencyResolver {
        private readonly TinyIoCContainer _container = new TinyIoCContainer();

        public object Resolve(Type serviceType) {
            if (serviceType == null)
                throw new ArgumentNullException("serviceType");

            return _container.Resolve(serviceType);
        }

        public void Register(Type serviceType, Type concreteType) {
            _container.Register(serviceType, concreteType);
        }

        public void Register(Type serviceType, Func<object> activator) {
            _container.Register(serviceType, (c, p) => activator());
        }

        public void Dispose() {
            _container.Dispose();
        }
    }
}
