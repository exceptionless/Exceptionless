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
using Foundatio.Jobs;
using Foundatio.Jobs.Hosting;
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
                CreateWebHostBuilder(args).Build().Run();
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

                    if (options.EnableHealthChecks) {
                        app.UseHealthChecks("/health", new HealthCheckOptions {
                            Predicate = hcr => hcr.Tags.Contains("Core") || hcr.Tags.Contains(jobName ?? "Job")
                        });
                    }

                    if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                        app.UseExceptionless(ExceptionlessClient.Default);

                    app.Use((context, func) => context.Response.WriteAsync($"Running Job: {jobName ?? "All"}"));
                    
                    // run startup actions registered in the container
                    if (options.EnableBootstrapStartupActions) {
                        var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
                        lifetime.ApplicationStarted.Register(() => {
                            var shutdownSource = new CancellationTokenSource();
                            Console.CancelKeyPress += (sender, args1) => {
                                shutdownSource.Cancel();
                                args1.Cancel = true;
                            };

                            var combined = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, shutdownSource.Token);
                            app.ApplicationServices.RunStartupActionsAsync(combined.Token).GetAwaiter().GetResult();
                        });
                    }
                });
            
            if (useApplicationInsights)
                builder.UseApplicationInsights(options.ApplicationInsightsKey);

            var metricOptions = container.GetRequiredService<IOptions<MetricOptions>>().Value;
            if (!String.IsNullOrEmpty(metricOptions.Provider))
                ConfigureMetricsReporting(builder, metricOptions);

            return builder;
        }

        private static void AddJobs(IServiceCollection serviceCollection, string jobName) {
            string job = jobName ?? "all";
            switch (job) {
                case "CleanupSnapshot":
                    serviceCollection.AddJob<CleanupSnapshotJob>();
                    break;
                case "CloseInactiveSessions":
                    serviceCollection.AddJob<CloseInactiveSessionsJob>();
                    break;
                case "DailySummary":
                    serviceCollection.AddJob<DailySummaryJob>();
                    break;
                case "DownloadGeoipDatabase":
                    serviceCollection.AddJob<DownloadGeoIPDatabaseJob>();
                    break;
                case "EventNotifications":
                    serviceCollection.AddJob<EventNotificationsJob>();
                    break;
                case "EventPosts":
                    serviceCollection.AddJob<EventPostsJob>();
                    break;
                case "EventSnapshot":
                    serviceCollection.AddJob<EventSnapshotJob>();
                    break;
                case "EventUserDescriptions":
                    serviceCollection.AddJob<EventUserDescriptionsJob>();
                    break;
                case "MailMessage":
                    serviceCollection.AddJob<MailMessageJob>();
                    break;
                case "MaintainIndexes":
                    serviceCollection.AddJob<MaintainIndexesJob>();
                    break;
                case "OrganizationSnapshot":
                    serviceCollection.AddJob<OrganizationSnapshotJob>();
                    break;
                case "RetentionLimits":
                    serviceCollection.AddJob<RetentionLimitsJob>();
                    break;
                case "StackEventCount":
                    serviceCollection.AddJob<StackEventCountJob>();
                    break;
                case "StackSnapshot":
                    serviceCollection.AddJob<StackSnapshotJob>();
                    break;
                case "WebHooks":
                    serviceCollection.AddJob<WebHooksJob>();
                    break;
                case "WorkItem":
                    serviceCollection.AddJob<WorkItemJob>();
                    break;
                case "all":
                    serviceCollection.AddJob<CleanupSnapshotJob>();
                    serviceCollection.AddJob<CloseInactiveSessionsJob>();
                    serviceCollection.AddJob<DailySummaryJob>();
                    serviceCollection.AddJob<DownloadGeoIPDatabaseJob>();
                    serviceCollection.AddJob<EventNotificationsJob>();
                    serviceCollection.AddJob<EventPostsJob>();
                    serviceCollection.AddJob<EventSnapshotJob>();
                    serviceCollection.AddJob<EventUserDescriptionsJob>();
                    serviceCollection.AddJob<MailMessageJob>();
                    serviceCollection.AddJob<MaintainIndexesJob>();
                    serviceCollection.AddJob<OrganizationSnapshotJob>();
                    serviceCollection.AddJob<RetentionLimitsJob>();
                    serviceCollection.AddJob<StackEventCountJob>();
                    serviceCollection.AddJob<StackSnapshotJob>();
                    serviceCollection.AddJob<WebHooksJob>();
                    serviceCollection.AddJob<WorkItemJob>();
                    break;
                default:
                    throw new ArgumentException($"Job not found: ${job}", nameof(jobName));
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
