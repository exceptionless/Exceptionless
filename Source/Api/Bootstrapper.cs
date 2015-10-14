using System;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Foundatio.Caching;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using SimpleInjector;
using SimpleInjector.Packaging;
using PrincipalUserIdProvider = Exceptionless.Api.Hubs.PrincipalUserIdProvider;

namespace Exceptionless.Api {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            // Register SignalR services.
            container.Register<IUserIdProvider, PrincipalUserIdProvider>();
            container.Register<MessageBusConnection>();
            container.RegisterSingleton<MessageBusBroker>();
            container.RegisterSingleton<ConnectionMapping>();
            container.RegisterSingleton<IConnectionManager>(GlobalHost.ConnectionManager);

            container.RegisterSingleton<OverageHandler>();
            container.RegisterSingleton<ThrottlingHandler>(() => new ThrottlingHandler(container.GetInstance<ICacheClient>(), userIdentifier => Settings.Current.ApiThrottleLimit, TimeSpan.FromMinutes(15)));
        }
    }
}