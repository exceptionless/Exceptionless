using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogLevel = Exceptionless.Logging.LogLevel;

namespace Exceptionless.Insulation.Jobs {
    public class JobServiceProvider {
        public static IServiceProvider CreateServiceProvider(ILoggerFactory loggerFactory) {
            loggerFactory.AddConsole();
            loggerFactory.AddExceptionless();

            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .Build();
            Settings.Initialize(config);

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

            Settings.Current.DisableIndexConfiguration = true;
            Core.Bootstrapper.RegisterServices(services, loggerFactory);
            Bootstrapper.RegisterServices(services, true, loggerFactory);

            var container = services.BuildServiceProvider();

            if (!Settings.Current.DisableBootstrapStartupActions)
                container.RunStartupActionsAsync().GetAwaiter().GetResult();

            return container;
        }
    }
}
