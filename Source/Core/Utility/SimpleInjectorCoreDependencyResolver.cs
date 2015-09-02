using System;
using System.Collections.Generic;
using Exceptionless.Core.Dependency;
using SimpleInjector;

namespace Exceptionless.Core.Utility {
    public class SimpleInjectorCoreDependencyResolver : IDependencyResolver {
        private readonly Container _container;

        public SimpleInjectorCoreDependencyResolver(Container container) {
            _container = container;
        }

        public object GetService(Type serviceType) {
            return _container.GetInstance(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType) {
            return _container.GetAllInstances(serviceType);
        }
    }
}