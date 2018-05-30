using System;
using System.IO;
using Exceptionless.Core;
using Exceptionless.Insulation.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Web {
    public class Program {
        public static int Main(string[] args) {
            try {
                CreateWebHostBuilder(args).Build().Run();
                return 0;
            } catch (Exception ex) {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                ExceptionlessClient.Default.ProcessQueue();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) {
            string environment = Environment.GetEnvironmentVariable("AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            string currentDirectory = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var settings = Settings.ReadFromConfiguration(config, environment);

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(settings.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

            Log.Logger = loggerConfig.CreateLogger();

            Log.Information("Bootstrapping {AppMode} mode API ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", environment, Settings.Current.InformationalVersion, Environment.MachineName, Settings.Current, currentDirectory);

            return WebHost.CreateDefaultBuilder(args)
                .UseEnvironment(environment)
                .UseKestrel(c => {
                    c.AddServerHeader = false;
                    if (Settings.Current.MaximumEventPostSize > 0)
                        c.Limits.MaxRequestBodySize = Settings.Current.MaximumEventPostSize;
                })
                .UseConfiguration(config)
                .ConfigureLogging(b => b.AddSerilog(Log.Logger))
                .ConfigureServices(s => {
                    s.AddSingleton(settings);
                })
                .UseStartup<Startup>();
        }
    }
}
