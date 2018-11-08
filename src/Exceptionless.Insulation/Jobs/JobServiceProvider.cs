using System;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Exceptionless.Logging.LogLevel;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Insulation.Jobs {
    public class JobServiceProvider {
        public static IServiceProvider GetServiceProvider() {
            AppDomain.CurrentDomain.SetDataDirectory();

            string environment = Environment.GetEnvironmentVariable("AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            string currentDirectory = AppContext.BaseDirectory;
            var config = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.ConfigureOptions<ConfigureAppOptions>();

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);

            var container = services.BuildServiceProvider();
            var options = container.GetRequiredService<IOptions<AppOptions>>().Value;
            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl)) {
                var client = ExceptionlessClient.Default;
                client.Configuration.SetDefaultMinLogLevel(LogLevel.Warn);
                client.Configuration.UseLogger(new SelfLogLogger());
                client.Configuration.SetVersion(options.Version);
                client.Configuration.UseInMemoryStorage();

                if (String.IsNullOrEmpty(options.InternalProjectId))
                    client.Configuration.Enabled = false;

                client.Configuration.ServerUrl = options.ExceptionlessServerUrl;
                client.Startup(options.ExceptionlessApiKey);

                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);
            }

            Log.Logger = loggerConfig.CreateLogger();
            Log.Information("Bootstrapping {AppMode} mode job ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", environment, options.InformationalVersion, Environment.MachineName, options, currentDirectory);

            services.AddLogging(b => b.AddSerilog(Log.Logger));
            Core.Bootstrapper.RegisterServices(services);
            Bootstrapper.RegisterServices(container, services, options, true);
            
            Core.Bootstrapper.LogConfiguration(container, options, container.GetRequiredService<ILoggerFactory>());
            if (options.EnableBootstrapStartupActions)
                container.RunStartupActionsAsync().GetAwaiter().GetResult();

            return container;
        }
    }
}
