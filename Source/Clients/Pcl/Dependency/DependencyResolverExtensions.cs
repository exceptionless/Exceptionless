using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Logging;
using Exceptionless.Queue;
using Exceptionless.Serializer;
using Exceptionless.Services;
using Exceptionless.Submission;

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

        public static IEventQueue GetEventQueue(this IDependencyResolver resolver) {
            return resolver.Resolve<IEventQueue>() ?? InMemoryEventQueue.Instance;
        }

        public static IExceptionlessLog GetLog(this IDependencyResolver resolver) {
            return resolver.Resolve<IExceptionlessLog>() ?? NullExceptionlessLog.Instance;
        }

        public static IEnvironmentInfoCollector GetEnvironmentInfoCollector(this IDependencyResolver resolver) {
            return resolver.Resolve<IEnvironmentInfoCollector>() ?? DefaultEnvironmentInfoCollector.Instance;
        }

        public static ILastClientIdManager GetLastErrorIdManager(this IDependencyResolver resolver) {
            return resolver.Resolve<ILastClientIdManager>() ?? DefaultLastClientIdManager.Instance;
        }

        public static IJsonSerializer GetJsonSerializer(this IDependencyResolver resolver) {
            return resolver.Resolve<IJsonSerializer>() ?? DefaultJsonSerializer.Instance;
        }

        public static ISubmissionClient GetSubmissionClient(this IDependencyResolver resolver) {
            return resolver.Resolve<ISubmissionClient>() ?? DefaultSubmissionClient.Instance;
        }
    }
}
