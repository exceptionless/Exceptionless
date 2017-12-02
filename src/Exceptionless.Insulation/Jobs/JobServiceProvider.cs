using System;
using System.Diagnostics;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogLevel = Exceptionless.Logging.LogLevel;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;
using ILogger = Serilog.ILogger;

namespace Exceptionless.Insulation.Jobs {
    public class JobServiceProvider {
        public static IServiceProvider GetServiceProvider() {
            AppDomain.CurrentDomain.SetDataDirectory();

            string environment = Environment.GetEnvironmentVariable("AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            Settings.Initialize(config, environment);
            Settings.Current.DisableIndexConfiguration = true;

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);

            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey) && !String.IsNullOrEmpty(Settings.Current.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.SetDefaultMinLogLevel(LogLevel.Warn);
                client.Configuration.UseLogger(new SelfLogLogger());
                client.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.FromSeconds(15);
                client.Configuration.SetVersion(Settings.Current.Version);
                client.Configuration.UseInMemoryStorage();

                client.Configuration.ServerUrl = Settings.Current.ExceptionlessServerUrl;
                client.Startup(Settings.Current.ExceptionlessApiKey);

                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Warning);
            }

            Log.Logger = loggerConfig.CreateLogger();
            Log.Information("Bootstrapping {AppMode} mode job ({InformationalVersion}) on {MachineName} using {@Settings}", environment, Settings.Current.InformationalVersion, Environment.MachineName, Settings.Current);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddSerilog(Log.Logger));
            services.AddSingleton<IConfiguration>(config);
            Core.Bootstrapper.RegisterServices(services);
            Bootstrapper.RegisterServices(services, true);

            var container = services.BuildServiceProvider();

            Core.Bootstrapper.LogConfiguration(container, container.GetRequiredService<ILoggerFactory>());
            if (Settings.Current.EnableBootstrapStartupActions)
                container.RunStartupActionsAsync().GetAwaiter().GetResult();

            return container;
        }
    }
}
