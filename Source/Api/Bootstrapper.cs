using System;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Foundatio.Caching;
using Foundatio.Logging;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using SimpleInjector;
using PrincipalUserIdProvider = Exceptionless.Api.Hubs.PrincipalUserIdProvider;

namespace Exceptionless.Api {
    public class Bootstrapper {
        public static void RegisterServices(Container container, ILoggerFactory loggerFactory) {
            container.Register<IUserIdProvider, PrincipalUserIdProvider>();
            container.Register<MessageBusConnection>();
            container.RegisterSingleton<ConnectionMapping>();
            container.RegisterSingleton<MessageBusBroker>();

            var resolver = new SimpleInjectorSignalRDependencyResolver(container);
            container.RegisterSingleton<IDependencyResolver>(resolver);
            container.RegisterSingleton<IConnectionManager>(() => new ConnectionManager(resolver));

            container.RegisterSingleton<OverageHandler>();
            container.RegisterSingleton<ThrottlingHandler>(() => new ThrottlingHandler(container.GetInstance<ICacheClient>(), userIdentifier => Settings.Current.ApiThrottleLimit, TimeSpan.FromMinutes(15)));
        }
    }
}