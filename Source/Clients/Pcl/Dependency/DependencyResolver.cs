using System;
using Exceptionless.Duplicates;
using Exceptionless.Logging;
using Exceptionless.Queue;
using Exceptionless.Serializer;
using Exceptionless.Services;
using Exceptionless.Storage;
using Exceptionless.Submission;

namespace Exceptionless.Dependency {
    public class DependencyResolver {
        private static readonly Lazy<IDependencyResolver> _defaultResolver = new Lazy<IDependencyResolver>(CreateDefault);

        private static IDependencyResolver _resolver;

        public static IDependencyResolver Default {
            get {
                return _resolver ?? _defaultResolver.Value;
            }
            set {
                _resolver = value;
            }
        }

        public static IDependencyResolver CreateDefault() {
            var resolver = new DefaultDependencyResolver();
            RegisterDefaultServices(resolver);
            return resolver;
        }

        public static void RegisterDefaultServices(IDependencyResolver resolver) {
            var exceptionlessLog = new Lazy<IExceptionlessLog>(() => resolver.Resolve<NullExceptionlessLog>());
            resolver.Register(typeof(IExceptionlessLog), () => exceptionlessLog.Value);

            var jsonSerializer = new Lazy<IJsonSerializer>(() => new DefaultJsonSerializer());
            resolver.Register(typeof(IJsonSerializer), () => jsonSerializer.Value);

            var eventQueue = new Lazy<IEventQueue>(() => new DefaultEventQueue());
            resolver.Register(typeof(IEventQueue), () => eventQueue.Value);

            var submissionClient = new Lazy<ISubmissionClient>(() => new DefaultSubmissionClient());
            resolver.Register(typeof(ISubmissionClient), () => submissionClient.Value);

            var keyValueStorage = new Lazy<IKeyValueStorage>(() => new InMemoryKeyValueStorage());
            resolver.Register(typeof(IKeyValueStorage), () => keyValueStorage.Value);

            var environmentInfoCollector = new Lazy<IEnvironmentInfoCollector>(() => new DefaultEnvironmentInfoCollector());
            resolver.Register(typeof(IEnvironmentInfoCollector), () => environmentInfoCollector.Value);

            var lastClientIdManager = new Lazy<ILastReferenceIdManager>(() => new DefaultLastReferenceIdManager());
            resolver.Register(typeof(ILastReferenceIdManager), () => lastClientIdManager.Value);

            var duplicateChecker = new Lazy<IDuplicateChecker>(() => new DefaultDuplicateChecker(resolver.GetLog()));
            resolver.Register(typeof(IDuplicateChecker), () => duplicateChecker.Value);
        }
    }
}
