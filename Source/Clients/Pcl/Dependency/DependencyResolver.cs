using System;
using Exceptionless.Logging;
using Exceptionless.Queue;
using Exceptionless.Serializer;
using Exceptionless.Services;
using Exceptionless.Storage;
using Exceptionless.Submission;

namespace Exceptionless.Dependency {
    public class DependencyResolver {
        private static readonly Lazy<IDependencyResolver> _defaultResolver = new Lazy<IDependencyResolver>(() => {
            var resolver = new DefaultDependencyResolver();
            RegisterDefaultServices(resolver);
            return resolver;
        });

        private static IDependencyResolver _resolver;

        public static IDependencyResolver Current {
            get {
                return _resolver ?? _defaultResolver.Value;
            }
            set {
                _resolver = value;
            }
        }

        public static void RegisterDefaultServices(IDependencyResolver resolver) {
            var exceptionlessLog = new Lazy<IExceptionlessLog>(() => new NullExceptionlessLog());
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

            var lastClientIdManager = new Lazy<ILastClientIdManager>(() => new DefaultLastClientIdManager());
            resolver.Register(typeof(ILastClientIdManager), () => lastClientIdManager.Value);

            //var serverIdManager = new ServerIdManager();
            //resolver.Register(typeof(IServerIdManager), () => serverIdManager);

            //var traceManager = new Lazy<TraceManager>(() => new TraceManager());
            //resolver.Register(typeof(IServerIdManager), () => traceManager.Value);
        }
    }
}
