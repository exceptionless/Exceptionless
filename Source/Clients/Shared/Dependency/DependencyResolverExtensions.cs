using System;
using Exceptionless.Duplicates;
using Exceptionless.Logging;
using Exceptionless.Queue;
using Exceptionless.Serializer;
using Exceptionless.Services;
using Exceptionless.Storage;
using Exceptionless.Submission;

namespace Exceptionless.Dependency {
    public static class DependencyResolverExtensions {
        public static bool HasRegistration<TService>(this IDependencyResolver resolver) where TService : class {
            if (resolver == null)
                return false;
            
            return resolver.Resolve(typeof(TService)) != null;
        }

        public static object Resolve(this IDependencyResolver resolver, Type type) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            if (type == null)
                throw new ArgumentNullException("type");

            return resolver.Resolve(type);
        }

        public static TService Resolve<TService>(this IDependencyResolver resolver, TService defaultImplementation = null) where TService : class {
            if (resolver == null)
                throw new ArgumentNullException("resolver");
            
            var serviceImpl = resolver.Resolve(typeof(TService));
            return serviceImpl as TService ?? defaultImplementation;
        }

        public static void Register<TService>(this IDependencyResolver resolver, TService implementation) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            resolver.Register(typeof(TService), () => implementation);
        }

        public static void Register<TService>(this IDependencyResolver resolver) {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            resolver.Register(typeof(TService), typeof(TService));
        }

        public static void Register<TService, TImplementation>(this IDependencyResolver resolver) where TImplementation : TService {
            if (resolver == null)
                throw new ArgumentNullException("resolver");

            resolver.Register(typeof(TService), typeof(TImplementation));
        }

        public static IExceptionlessLog GetLog(this IDependencyResolver resolver) {
            return resolver.Resolve<IExceptionlessLog>() ?? resolver.Resolve<NullExceptionlessLog>();
        }

        public static IJsonSerializer GetJsonSerializer(this IDependencyResolver resolver) {
            return resolver.Resolve<IJsonSerializer>() ?? resolver.Resolve<DefaultJsonSerializer>();
        }

        public static IEventQueue GetEventQueue(this IDependencyResolver resolver) {
            return resolver.Resolve<IEventQueue>() ?? resolver.Resolve<DefaultEventQueue>();
        }

        public static ISubmissionClient GetSubmissionClient(this IDependencyResolver resolver) {
            return resolver.Resolve<ISubmissionClient>() ?? resolver.Resolve<DefaultSubmissionClient>();
        }

        public static IObjectStorage GetFileStorage(this IDependencyResolver resolver) {
            return resolver.Resolve<IObjectStorage>() ?? resolver.Resolve<InMemoryObjectStorage>();
        }

        public static IEnvironmentInfoCollector GetEnvironmentInfoCollector(this IDependencyResolver resolver) {
            return resolver.Resolve<IEnvironmentInfoCollector>() ?? resolver.Resolve<DefaultEnvironmentInfoCollector>();
        }

        public static ILastReferenceIdManager GetLastReferenceIdManager(this IDependencyResolver resolver) {
            return resolver.Resolve<ILastReferenceIdManager>() ?? resolver.Resolve<DefaultLastReferenceIdManager>();
        }

        public static IDuplicateChecker GetDuplicateChecker(this IDependencyResolver resolver) {
            return resolver.Resolve<IDuplicateChecker>() ?? resolver.Resolve<DefaultDuplicateChecker>();
        }
    }
}
