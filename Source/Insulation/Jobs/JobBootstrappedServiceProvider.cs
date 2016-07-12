﻿using System;
using Exceptionless.Core;
using Exceptionless.NLog;
using Foundatio.Logging;
using Foundatio.ServiceProviders;
using SimpleInjector;
using SimpleInjector.Extensions.ExecutionContextScoping;

namespace Exceptionless.Insulation.Jobs {
    public class JobBootstrappedServiceProvider : BootstrappedServiceProviderBase {
        protected override IServiceProvider BootstrapInternal(ILoggerFactory loggerFactory) {
            ExceptionlessClient.Default.Configuration.SetVersion(Settings.Current.Version);
            ExceptionlessClient.Default.Configuration.UseLogger(new NLogExceptionlessLog(Exceptionless.Logging.LogLevel.Warn));
            ExceptionlessClient.Default.Register();

            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.DefaultScopedLifestyle = new ExecutionContextScopeLifestyle();
            container.Options.ResolveUnregisteredCollections = true;

            Core.Bootstrapper.RegisterServices(container, loggerFactory);
            Bootstrapper.RegisterServices(container, loggerFactory);

#if DEBUG
            container.Verify();
#endif

            return container;
        }
    }
}
