using System;
using System.IO;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters;
using App.Metrics.Formatters.Prometheus;
using Exceptionless.Core;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Web.Utility;
using Microsoft.ApplicationInsights.Extensibility;
using Exceptionless.Insulation.Metrics;
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

            bool useApplicationInsights = !String.IsNullOrEmpty(Settings.Current.ApplicationInsightsKey);

            var builder = WebHost.CreateDefaultBuilder(args)
                .UseEnvironment(environment)
                .UseKestrel(c => {
                    c.AddServerHeader = false;
                    if (Settings.Current.MaximumEventPostSize > 0)
                        c.Limits.MaxRequestBodySize = Settings.Current.MaximumEventPostSize;
                })
                .UseSerilog(Log.Logger)
                .SuppressStatusMessages(true)
                .UseConfiguration(config)
                .ConfigureServices(s => {
                    if (useApplicationInsights) {
                        s.AddSingleton<ITelemetryInitializer, ExceptionlessTelemetryInitializer>();
                        s.AddHttpContextAccessor();
                        s.AddApplicationInsightsTelemetry();
                    }
                    s.AddSingleton(settings);
                })
                .UseStartup<Startup>();

            if (useApplicationInsights)
                builder.UseApplicationInsights(Settings.Current.ApplicationInsightsKey);

            if (settings.EnableMetricsReporting) {
                settings.MetricsConnectionString = MetricsConnectionString.Parse(settings.MetricsConnectionString?.ConnectionString);
                ConfigureMetricsReporting(builder);
            }

            return builder;
        }

        private static void ConfigureMetricsReporting(IWebHostBuilder builder) {
            if (Settings.Current.MetricsConnectionString is PrometheusMetricsConnectionString) {
                var metrics = AppMetrics.CreateDefaultBuilder()
                    .OutputMetrics.AsPrometheusPlainText()
                    .OutputMetrics.AsPrometheusProtobuf()
                    .Build();
                builder.ConfigureMetrics(metrics).UseMetrics(options => {
                    options.EndpointOptions = endpointsOptions => {
                        endpointsOptions.MetricsTextEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                        endpointsOptions.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusProtobufOutputFormatter>();
                    };
                });
            } else if (!(Settings.Current.MetricsConnectionString is StatsDMetricsConnectionString)) {
                builder.UseMetrics();
            }
        }
    }
}
