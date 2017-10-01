using System;
using Exceptionless.Core;
using Foundatio.Jobs;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using LogLevel = Exceptionless.Logging.LogLevel;

namespace Exceptionless.Insulation.Jobs {
    public class JobServiceProvider {
        public static IServiceProvider CreateServiceProvider(ILoggerFactory loggerFactory) {
            loggerFactory.AddConsole();
            loggerFactory.AddExceptionless();

            var shutdownCancellationToken = JobRunner.GetShutdownCancellationToken();

            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                //client.Configuration.UseLogger(new NLogExceptionlessLog(LogLevel.Warn));
                client.Configuration.SetDefaultMinLogLevel(LogLevel.Warn);
                client.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.FromSeconds(15);
                client.Configuration.SetVersion(Settings.Current.Version);
                client.Configuration.UseInMemoryStorage();

                client.Configuration.ServerUrl = Settings.Current.ExceptionlessServerUrl;
                client.Startup(Settings.Current.ExceptionlessApiKey);
            }

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
