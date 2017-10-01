using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

        public static async Task RunStartupActionsAsync(this Container container) {
            foreach (var startupAction in container.GetAllInstances<IStartupAction>())
                await startupAction.RunAsync().AnyContext();
        }

        public static void AddStartupAction<T>(this Container container) where T : IStartupAction {
            container.AppendToCollection(typeof(IStartupAction), Lifestyle.Transient.CreateRegistration(typeof(T), container));
        }

        public static void AddStartupAction(this Container container, Action action) {
            container.AppendToCollection(typeof(IStartupAction), Lifestyle.Transient.CreateRegistration(typeof(IStartupAction), () => new StartupAction(() => {
                action();
                return Task.FromResult(0);
            }), container));
        }

        public static void AddStartupAction(this Container container, Func<Task> action) {
            container.AppendToCollection(typeof(IStartupAction), Lifestyle.Transient.CreateRegistration(typeof(IStartupAction), () => new StartupAction(action), container));
        }

        private class StartupAction : IStartupAction {
            private readonly Func<Task> _action;

            public StartupAction(Func<Task> action) {
                _action = action;
            }

            public Task RunAsync() {
                return _action();
            }
        }
    }

    public interface IStartupAction {
        Task RunAsync();
    }
}