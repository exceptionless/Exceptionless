#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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