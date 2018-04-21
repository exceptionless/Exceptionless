using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;
using Exceptionless.Insulation.Configuration;

namespace Exceptionless.Job {
    public class Program {
        public static async Task<int> Main(string[] args) {
            try {
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

                Settings.Initialize(config, environment);
                Settings.Current.DisableIndexConfiguration = true;

                var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
                if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey))
                    loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

                Log.Logger = loggerConfig.CreateLogger();

                Log.Information("Bootstrapping {AppMode} mode job ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", environment, Settings.Current.InformationalVersion, Environment.MachineName, Settings.Current, currentDirectory);

                var builder = new HostBuilder()
                    .UseEnvironment(environment)
                    .ConfigureAppConfiguration(c => c.AddConfiguration(config))
                    .ConfigureLogging(b => b.AddSerilog(Log.Logger))
                    .ConfigureServices(s => {
                        Bootstrapper.RegisterServices(s);
                        Insulation.Bootstrapper.RegisterServices(s, true);
                    });

                await builder.RunConsoleAsync();
                return 0;
            } catch (Exception ex) {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                await ExceptionlessClient.Default.ProcessQueueAsync();
            }
        }
    }
}
