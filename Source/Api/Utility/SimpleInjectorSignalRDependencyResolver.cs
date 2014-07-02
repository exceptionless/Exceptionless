using System;
using Microsoft.AspNet.SignalR;
using SimpleInjector;

namespace Exceptionless.Api.Utility {
    public class SimpleInjectorSignalRDependencyResolver : DefaultDependencyResolver {
        private readonly Container _container;

        public SimpleInjectorSignalRDependencyResolver(Container container) {
            _container = container;
        }

        public override object GetService(Type serviceType) {
            return ((IServiceProvider)_container).GetService(serviceType) ?? base.GetService(serviceType);
        }
    }
}