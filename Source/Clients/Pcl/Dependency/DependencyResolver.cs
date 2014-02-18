using System;
using Exceptionless.Logging;

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

        internal static IExceptionlessLog Log { get { return Current.Resolve<IExceptionlessLog>() ?? NullExceptionlessLog.Instance; } }

        public static void RegisterDefaultServices(IDependencyResolver resolver) {
            resolver.Register(typeof(IExceptionlessLog), () => NullExceptionlessLog.Instance);

            //var serverIdManager = new ServerIdManager();
            //resolver.Register(typeof(IServerIdManager), () => serverIdManager);

            //var traceManager = new Lazy<TraceManager>(() => new TraceManager());
            //resolver.Register(typeof(IServerIdManager), () => traceManager.Value);
        }
    }
}
