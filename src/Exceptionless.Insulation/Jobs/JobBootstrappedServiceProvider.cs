using System;
using Exceptionless.Core;
using Exceptionless.NLog;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.ServiceProviders;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Exceptionless.Insulation.Jobs {
    public class JobBootstrappedServiceProvider : BootstrappedServiceProviderBase {
        protected override IServiceProvider BootstrapInternal(ILoggerFactory loggerFactory) {
            var shutdownCancellationToken = JobRunner.GetShutdownCancellationToken();

            ExceptionlessClient.Default.Configuration.SetVersion(Settings.Current.Version);
            ExceptionlessClient.Default.Configuration.UseLogger(new NLogExceptionlessLog(Exceptionless.Logging.LogLevel.Warn));
            ExceptionlessClient.Default.Startup();

            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            Settings.Current.DisableIndexConfiguration = true;
            Core.Bootstrapper.RegisterServices(container, loggerFactory, shutdownCancellationToken);
            Bootstrapper.RegisterServices(container, true, loggerFactory, shutdownCancellationToken);

#if DEBUG
            container.Verify();
#endif

            return container;
        }
    }
}
