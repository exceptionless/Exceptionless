using System;
using Exceptionless.Api.Hubs;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Foundatio.Caching;
using Microsoft.AspNet.SignalR;
using SimpleInjector;
using SimpleInjector.Packaging;

namespace Exceptionless.Api {
    public class Bootstrapper : IPackage {
        public void RegisterServices(Container container) {
            container.RegisterSingle<IUserIdProvider, PrincipalUserIdProvider>();
            container.RegisterSingle<MessageBusHub>();
            container.Register<OverageHandler>();
            container.Register<ThrottlingHandler>(() => new ThrottlingHandler(container.GetInstance<ICacheClient>(), userIdentifier => Settings.Current.ApiThrottleLimit, TimeSpan.FromMinutes(15)));
        }
    }
}