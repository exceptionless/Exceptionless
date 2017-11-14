using System;
using System.Diagnostics;
using System.IO;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Insulation.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Web {
    public class Program {
        public static int Main(string[] args) {
            string environment = Environment.GetEnvironmentVariable("AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            Settings.Initialize(config, environment);

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Warning);

            Log.Logger = loggerConfig.CreateLogger();

            try {
                Log.Information("Bootstrapping {AppMode} mode API ({InformationalVersion}) on {MachineName} using {@Settings}", environment, Settings.Current.InformationalVersion, Environment.MachineName, Settings.Current);

                var webHost = new WebHostBuilder()
                    .UseEnvironment(environment)
                    .UseKestrel(c => c.AddServerHeader = false)
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseConfiguration(config)
                    .ConfigureLogging(b => b.AddSerilog(Log.Logger))
                    .UseIISIntegration()
                    .UseStartup<Startup>()
                    .Build();

                webHost.Run();
                return 0;
            } catch (Exception ex) {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                ExceptionlessClient.Default.ProcessQueue();
            }
        }
    }
}
