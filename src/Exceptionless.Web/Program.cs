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
using Foundatio.Hosting;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Web {
    public class Program {
        private static Microsoft.Extensions.Logging.ILogger _logger;
        
        public static async Task<int> Main(string[] args) {
            try {
                await CreateWebHostBuilder(args).Build().RunAsync(_logger);
                return 0;
            } catch (Exception ex) {
                _logger.LogCritical(ex, "Job host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                await ExceptionlessClient.Default.ProcessQueueAsync();
                
                if (Debugger.IsAttached)
                    Console.ReadKey();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) {
            string environment = Environment.GetEnvironmentVariable("EX_AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            Console.Title = "Exceptionless Web";

            string currentDirectory = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("EX_")
                .AddCommandLine(args)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.ConfigureOptions<ConfigureAppOptions>();
            services.ConfigureOptions<ConfigureMetricOptions>();
            var container = services.BuildServiceProvider();
            var options = container.GetRequiredService<IOptions<AppOptions>>().Value;

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

            var loggerFactory = new SerilogLoggerFactory(loggerConfig.CreateLogger());
            _logger = loggerFactory.CreateLogger<Program>();
            
            var configDictionary = config.ToDictionary("Serilog");
            _logger.LogInformation("Bootstrapping Exceptionless Web in {AppMode} mode ({InformationalVersion}) on {MachineName} with settings {@Settings}", environment, options.InformationalVersion, Environment.MachineName, configDictionary, currentDirectory);

            bool useApplicationInsights = !String.IsNullOrEmpty(options.ApplicationInsightsKey);

            var builder = WebHost.CreateDefaultBuilder(args)
                .UseEnvironment(environment)
                .ConfigureKestrel(c => {
                    c.AddServerHeader = false;
                    c.AllowSynchronousIO = false;
                    
                    if (options.MaximumEventPostSize > 0)
                        c.Limits.MaxRequestBodySize = options.MaximumEventPostSize;
                })
                .UseConfiguration(config)
                .ConfigureServices(s => {
                    s.AddSingleton<ILoggerFactory>(loggerFactory);
                    s.AddHttpContextAccessor();
                    
                    if (useApplicationInsights) {
                        s.AddSingleton<ITelemetryInitializer, ExceptionlessTelemetryInitializer>();
                        s.AddApplicationInsightsTelemetry();
                    }
                })
                .UseStartup<Startup>();

            if (useApplicationInsights)
                builder.UseApplicationInsights(options.ApplicationInsightsKey);

            var metricOptions = container.GetRequiredService<IOptions<MetricOptions>>().Value;
            if (!String.IsNullOrEmpty(metricOptions.Provider))
                ConfigureMetricsReporting(builder, metricOptions);

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
