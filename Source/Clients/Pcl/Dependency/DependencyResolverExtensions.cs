using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Logging;
using Exceptionless.Serializer;
using Exceptionless.Services;

namespace Exceptionless.Dependency {
    public static class DependencyResolverExtensions {
        public static object Resolve(this IDependencyResolver resolver, Type type) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            if (type == null)
                throw new ArgumentNullException("type");

            return resolver.GetService(type);
        }

        public static TService Resolve<TService>(this IDependencyResolver resolver, TService defaultImplementation = null) where TService : class {
            if (resolver == null)
                throw new ArgumentNullException("resolver");
            
            var serviceImpl = resolver.GetService(typeof(TService));
            return serviceImpl as TService ?? defaultImplementation;
        }

        public static IEnumerable<object> ResolveAll(this IDependencyResolver resolver, Type type) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            if (type == null)
                throw new ArgumentNullException("type");

            return resolver.GetServices(type);
        }

        public static IEnumerable<TService> ResolveAll<TService>(this IDependencyResolver resolver) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            return resolver.GetServices(typeof(TService)).Cast<TService>();
        }

        public static void Register<TService>(this IDependencyResolver resolver, TService implementation) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            resolver.Register(typeof(TService), () => implementation);
        }

        internal static IExceptionlessLog GetLog(this IDependencyResolver resolver) {
            return resolver.Resolve<IExceptionlessLog>() ?? NullExceptionlessLog.Instance;
        }

        internal static IEnvironmentInfoCollector GetEnvironmentInfoCollector(this IDependencyResolver resolver) {
            return resolver.Resolve<IEnvironmentInfoCollector>() ?? DefaultEnvironmentInfoCollector.Instance;
        }

        internal static ILastErrorIdManager GetLastErrorIdManager(this IDependencyResolver resolver) {
            return resolver.Resolve<ILastErrorIdManager>() ?? DefaultLastErrorIdManager.Instance;
        }

        internal static IJsonSerializer GetJsonSerializer(this IDependencyResolver resolver) {
            return resolver.Resolve<IJsonSerializer>() ?? DefaultJsonSerializer.Instance;
        }
    }
}
