using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters;
using App.Metrics.Formatters.Prometheus;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Web.Utility;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Web {
    public class Program {
        public static async Task<int> Main(string[] args) {
            try {
                await CreateHostBuilder(args).Build().RunAsync();
                return 0;
            } catch (Exception ex) {
                Log.Fatal(ex, "Job host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                await ExceptionlessClient.Default.ProcessQueueAsync();

                if (Debugger.IsAttached)
                    Console.ReadKey();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) {
            string environment = Environment.GetEnvironmentVariable("EX_AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("EX_")
                .AddEnvironmentVariables("ASPNETCORE_")
                .AddCommandLine(args)
                .Build();

            return CreateHostBuilder(config, environment);
        }

        public static IHostBuilder CreateHostBuilder(IConfiguration config, string environment) {
            Console.Title = "Exceptionless Web";

            var options = AppOptions.ReadFromConfiguration(config);

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

            Log.Logger = loggerConfig.CreateLogger();
            var configDictionary = config.ToDictionary("Serilog");
            Log.Information("Bootstrapping Exceptionless Web in {AppMode} mode ({InformationalVersion}) on {MachineName} with settings {@Settings}", environment, options.InformationalVersion, Environment.MachineName, configDictionary);

            bool useApplicationInsights = !String.IsNullOrEmpty(options.ApplicationInsightsKey);
            var builder = Host.CreateDefaultBuilder()
                .UseEnvironment(environment)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder
                        .UseConfiguration(config)
                        .ConfigureKestrel(c => {
                            c.AddServerHeader = false;
                            // c.AllowSynchronousIO = false; // TODO: Investigate issue with JSON Serialization.

                            if (options.MaximumEventPostSize > 0)
                                c.Limits.MaxRequestBodySize = options.MaximumEventPostSize;
                        })
                        .UseStartup<Startup>();

                    var metricOptions = MetricOptions.ReadFromConfiguration(config);
                    if (!String.IsNullOrEmpty(metricOptions.Provider))
                        ConfigureMetricsReporting(webBuilder, metricOptions);
                })
                .ConfigureServices((ctx, services) => {
                    services.AddSingleton(config);
                    services.AddAppOptions(options);
                    services.AddHttpContextAccessor();

                    if (useApplicationInsights) {
                        services.AddSingleton<ITelemetryInitializer, ExceptionlessTelemetryInitializer>();
                        services.AddApplicationInsightsTelemetry(options.ApplicationInsightsKey);
                    }
                });

            return builder;
        }

        private static void ConfigureMetricsReporting(IWebHostBuilder builder, MetricOptions options) {
            if (String.Equals(options.Provider, "prometheus")) {
                var metrics = AppMetrics.CreateDefaultBuilder()
                    .OutputMetrics.AsPrometheusPlainText()
                    .OutputMetrics.AsPrometheusProtobuf()
                    .Build();
                builder.ConfigureMetrics(metrics).UseMetrics(o => {
                    o.EndpointOptions = endpointsOptions => {
                        endpointsOptions.MetricsTextEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                        endpointsOptions.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusProtobufOutputFormatter>();
                    };
                });
            } else if (!String.Equals(options.Provider, "statsd")) {
                builder.UseMetrics();
            }
        }
    }
}
