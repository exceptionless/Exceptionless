using System;
using System.IO;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters;
using App.Metrics.Formatters.Prometheus;
using App.Metrics.Reporting.Graphite;
using App.Metrics.Reporting.Http;
using App.Metrics.Reporting.InfluxDB;
using Exceptionless.Core;
using Exceptionless.Core.Utility;
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
                    s.AddSingleton(settings);
                })
                .UseStartup<Startup>();

            if (!String.IsNullOrEmpty(settings.ApplicationInsightsKey))
                builder.UseApplicationInsights(settings.ApplicationInsightsKey);

            if (settings.MetricsConnectionString != null && !(settings.MetricsConnectionString is StatsDMetricsConnectionString)) {
                // We have to configure the reporters here
                var metrics = BuildAppMetrics(settings);
                builder = builder.ConfigureMetrics(metrics).UseMetrics(options => ConfigureAppMetrics(settings, metrics, options));
            }

            return builder;
        }

        private static IMetricsRoot BuildAppMetrics(Settings settings) {
            var metricsBuilder = AppMetrics.CreateDefaultBuilder();
            switch (settings.MetricsConnectionString) {
                case InfuxDBMetricsConnectionString influxConnectionString:
                    metricsBuilder.Report.ToInfluxDb(new MetricsReportingInfluxDbOptions {
                        InfluxDb = {
                            BaseUri = new Uri(influxConnectionString.ServerUrl),
                            UserName = influxConnectionString.UserName,
                            Password = influxConnectionString.Password,
                            Database = influxConnectionString.Database
                        }
                    });
                    break;
                case HttpMetricsConnectionString httpConnectionString:
                    metricsBuilder.Report.OverHttp(new MetricsReportingHttpOptions {
                        HttpSettings = {
                            RequestUri = new Uri(httpConnectionString.ServerUrl),
                            UserName = httpConnectionString.UserName,
                            Password = httpConnectionString.Password
                        }
                    });
                    break;
                case PrometheusMetricsConnectionString prometheusConnectionString:
                    metricsBuilder.OutputMetrics.AsPrometheusPlainText();
                    metricsBuilder.OutputMetrics.AsPrometheusProtobuf();
                    break;
                case GraphiteMetricsConnectionString graphiteConnectionString:
                    metricsBuilder.Report.ToGraphite(new MetricsReportingGraphiteOptions {
                        Graphite = {
                            BaseUri = new Uri(graphiteConnectionString.ServerUrl)
                        }
                    });
                    break;
            }
            return metricsBuilder.Build();
        }

        private static void ConfigureAppMetrics(Settings settings, IMetricsRoot metrics, MetricsWebHostOptions options) {
            if (settings.MetricsConnectionString is PrometheusMetricsConnectionString) {
                options.EndpointOptions = endpointsOptions => {
                    endpointsOptions.MetricsTextEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                    endpointsOptions.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters.GetType<MetricsPrometheusProtobufOutputFormatter>();
                };
            }
        }
    }
}
