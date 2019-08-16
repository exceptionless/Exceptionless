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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
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

            string currentDirectory = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(currentDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("EX_")
                .AddCommandLine(args)
                .Build();

            return CreateWebHostBuilder(config, environment);
        }
        
        public static IWebHostBuilder CreateWebHostBuilder(IConfiguration config, string environment) {
            Console.Title = "Exceptionless Web";
            
            var options = AppOptions.ReadFromConfiguration(config);
            
            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

            var serilogLogger = loggerConfig.CreateLogger();
            _logger = new SerilogLoggerFactory(serilogLogger).CreateLogger<Program>();

            var configDictionary = config.ToDictionary("Serilog");
            _logger.LogInformation("Bootstrapping Exceptionless Web in {AppMode} mode ({InformationalVersion}) on {MachineName} with settings {@Settings}", environment, options.InformationalVersion, Environment.MachineName, configDictionary);

            bool useApplicationInsights = !String.IsNullOrEmpty(options.ApplicationInsightsKey);

            var builder = WebHost.CreateDefaultBuilder()
                .UseEnvironment(environment)
                .UseConfiguration(config)
                .UseDefaultServiceProvider((ctx, o) => {
                    o.ValidateScopes = ctx.HostingEnvironment.IsDevelopment();
                })
                .ConfigureKestrel(c => {
                    c.AddServerHeader = false;
                    // c.AllowSynchronousIO = false; // TODO: Investigate issue with JSON Serialization.
                    
                    if (options.MaximumEventPostSize > 0)
                        c.Limits.MaxRequestBodySize = options.MaximumEventPostSize;
                })
                .UseSerilog(serilogLogger, true)
                .ConfigureServices((ctx, services) => {
                    services.AddSingleton(config);
                    services.AddHttpContextAccessor();
                    
                    if (useApplicationInsights) {
                        services.AddSingleton<ITelemetryInitializer, ExceptionlessTelemetryInitializer>();
                        services.AddApplicationInsightsTelemetry();
                    }
                    
                    services.PostConfigure<HostFilteringOptions>(o => {
                        if (o.AllowedHosts == null || o.AllowedHosts.Count == 0) {
                            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
                            var hosts = ctx.Configuration["AllowedHosts"]?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            // Fall back to "*" to disable.
                            o.AllowedHosts = (hosts?.Length > 0 ? hosts : new[] { "*" });
                        }
                    });
                    
                    services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(new ConfigurationChangeTokenSource<HostFilteringOptions>(ctx.Configuration));
                    services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();
                })
                .UseStartup<Startup>();
            
            if (String.IsNullOrEmpty(builder.GetSetting(WebHostDefaults.ContentRootKey)))
                builder.UseContentRoot(Directory.GetCurrentDirectory());
            
            if (useApplicationInsights)
                builder.UseApplicationInsights(options.ApplicationInsightsKey);

            var metricOptions = MetricOptions.ReadFromConfiguration(config);
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

        internal class HostFilteringStartupFilter : IStartupFilter {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) {
                return app => {
                    app.UseHostFiltering();
                    next(app);
                };
            }
        }
    }
}
