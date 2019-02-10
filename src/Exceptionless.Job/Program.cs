using System;
using System.IO;
using System.Linq;
using System.Threading;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters;
using App.Metrics.Formatters.Prometheus;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Exceptionless.Insulation.Configuration;
using Foundatio.Hosting.Jobs;
using Foundatio.Hosting.Startup;
using Foundatio.Jobs;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Job {
    public class Program {
        public static int Main(string[] args) {
            try {
                CreateWebHostBuilder(args).RunJobHost();
                return 0;
            } catch (Exception ex) {
                Log.Fatal(ex, "Job host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                ExceptionlessClient.Default.ProcessQueue();
            }
        }
        
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) {
            string jobName = args.FirstOrDefault();
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

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.ConfigureOptions<ConfigureAppOptions>();
            services.ConfigureOptions<ConfigureMetricOptions>();
            var container = services.BuildServiceProvider();
            var options = container.GetRequiredService<IOptions<AppOptions>>().Value;

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

            Log.Logger = loggerConfig.CreateLogger();
            var configDictionary = config.ToDictionary();
            Log.Information("Bootstrapping {AppMode} mode job ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", environment, options.InformationalVersion, Environment.MachineName, configDictionary, currentDirectory);

            bool useApplicationInsights = !String.IsNullOrEmpty(options.ApplicationInsightsKey);

            var builder = WebHost.CreateDefaultBuilder(args)
                .UseEnvironment(environment)
                .UseKestrel(c => {
                    c.AddServerHeader = false;
                })
                .UseSerilog(Log.Logger)
                .SuppressStatusMessages(true)
                .UseConfiguration(config)
                .ConfigureServices(s => {
                    s.AddHttpContextAccessor();
                    
                    s.AddJobLifetime();
                    AddJobs(s, jobName);
                    
                    if (useApplicationInsights)
                        s.AddApplicationInsightsTelemetry();
                    
                    Core.Bootstrapper.RegisterServices(s);
                    var serviceProvider = s.BuildServiceProvider();
                    Insulation.Bootstrapper.RegisterServices(serviceProvider, s, options, true);
                })
                .Configure(app => {
                    var loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();
                    Core.Bootstrapper.LogConfiguration(app.ApplicationServices, options, loggerFactory);

                    if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                        app.UseExceptionless(ExceptionlessClient.Default);
                    
                    if (options.EnableHealthChecks) {
                        app.UseHealthChecks("/health", new HealthCheckOptions {
                            Predicate = hcr => hcr.Tags.Contains("Core") || hcr.Tags.Contains(jobName ?? "Job")
                        });
                    }
                    
                    if (options.EnableBootstrapStartupActions)
                        app.UseStartupMiddleware();
                    
                    app.Use((context, func) => context.Response.WriteAsync($"Running Job: {jobName ?? "All"}"));
                });
            
            if (useApplicationInsights)
                builder.UseApplicationInsights(options.ApplicationInsightsKey);

            var metricOptions = container.GetRequiredService<IOptions<MetricOptions>>().Value;
            if (!String.IsNullOrEmpty(metricOptions.Provider))
                ConfigureMetricsReporting(builder, metricOptions);

            return builder;
        }

        private static void AddJobs(IServiceCollection serviceCollection, string jobName) {
            switch (jobName) {
                case "CleanupSnapshot":
                    serviceCollection.AddJob<CleanupSnapshotJob>(true);
                    break;
                case "CloseInactiveSessions":
                    serviceCollection.AddJob<CloseInactiveSessionsJob>(true);
                    break;
                case "DailySummary":
                    serviceCollection.AddJob<DailySummaryJob>(true);
                    break;
                case "DownloadGeoipDatabase":
                    serviceCollection.AddJob<DownloadGeoIPDatabaseJob>(true);
                    break;
                case "EventNotifications":
                    serviceCollection.AddJob<EventNotificationsJob>(true);
                    break;
                case "EventPosts":
                    serviceCollection.AddJob<EventPostsJob>(true);
                    break;
                case "EventSnapshot":
                    serviceCollection.AddJob<EventSnapshotJob>(true);
                    break;
                case "EventUserDescriptions":
                    serviceCollection.AddJob<EventUserDescriptionsJob>(true);
                    break;
                case "MailMessage":
                    serviceCollection.AddJob<MailMessageJob>(true);
                    break;
                case "MaintainIndexes":
                    serviceCollection.AddJob<MaintainIndexesJob>(true);
                    break;
                case "OrganizationSnapshot":
                    serviceCollection.AddJob<OrganizationSnapshotJob>(true);
                    break;
                case "RetentionLimits":
                    serviceCollection.AddJob<RetentionLimitsJob>(true);
                    break;
                case "StackEventCount":
                    serviceCollection.AddJob<StackEventCountJob>(true);
                    break;
                case "StackSnapshot":
                    serviceCollection.AddJob<StackSnapshotJob>(true);
                    break;
                case "WebHooks":
                    serviceCollection.AddJob<WebHooksJob>(true);
                    break;
                case "WorkItem":
                    serviceCollection.AddJob<WorkItemJob>(true);
                    break;
                case null:
                    serviceCollection.AddJob<CleanupSnapshotJob>(true);
                    serviceCollection.AddJob<CloseInactiveSessionsJob>(true);
                    serviceCollection.AddJob<DailySummaryJob>(true);
                    serviceCollection.AddJob<DownloadGeoIPDatabaseJob>(true);
                    serviceCollection.AddJob<EventNotificationsJob>(true);
                    serviceCollection.AddJob<EventPostsJob>(true);
                    serviceCollection.AddJob<EventSnapshotJob>(true);
                    serviceCollection.AddJob<EventUserDescriptionsJob>(true);
                    serviceCollection.AddJob<MailMessageJob>(true);
                    serviceCollection.AddJob<MaintainIndexesJob>(true);
                    serviceCollection.AddJob<OrganizationSnapshotJob>(true);
                    serviceCollection.AddJob<RetentionLimitsJob>(true);
                    serviceCollection.AddJob<StackEventCountJob>(true);
                    serviceCollection.AddJob<StackSnapshotJob>(true);
                    serviceCollection.AddJob<WebHooksJob>(true);
                    serviceCollection.AddJob<WorkItemJob>(true);
                    break;
                default:
                    throw new ArgumentException($"Job not found: ${jobName}", nameof(jobName));
            }
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
