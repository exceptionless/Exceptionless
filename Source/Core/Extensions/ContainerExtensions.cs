using System;
using Foundatio.Logging;
using SimpleInjector;
using SimpleInjector.Advanced;

namespace Exceptionless.Core.Extensions {
    public static class ContainerExtensions {
        public static void RegisterLogger(this IServiceProvider serviceProvider, ILoggerFactory loggerFactory) {
            var container = serviceProvider as Container;
            if (container == null)
                return;

            container.RegisterSingleton<ILoggerFactory>(loggerFactory);
            container.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
        }

        public static void Bootstrap<T>(this Container container, T target) {
            foreach (var configuration in container.GetAllInstances<Action<T>>()) {
                configuration(target);
            }
        }

        public static void AddBootstrapper<T>(this Container container, Action<T> configuration) {
            var tran = Lifestyle.Transient;
            var type = typeof(Action<T>);

            container.AppendToCollection(type, tran.CreateRegistration(type, () => configuration, container));
        }
    }
}