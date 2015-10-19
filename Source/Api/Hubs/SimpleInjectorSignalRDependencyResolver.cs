using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.SignalR;
using SimpleInjector;

namespace Exceptionless.Api.Hubs {
    public class SimpleInjectorSignalRDependencyResolver : DefaultDependencyResolver {
        private readonly Container _container;

        public SimpleInjectorSignalRDependencyResolver(Container container) {
            _container = container;
        }

        public override object GetService(Type serviceType) {
            return ((IServiceProvider)_container).GetService(serviceType) ?? base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType) {
            return _container.GetAllInstances(serviceType).Concat(base.GetServices(serviceType));
        }
    }
}