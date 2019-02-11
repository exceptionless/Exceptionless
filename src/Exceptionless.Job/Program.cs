using System;
using System.Diagnostics;
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
                if (Debugger.IsAttached)
                    Console.ReadKey();
            }
        }
        
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) {
            var jobOptions = new JobRunnerOptions(args);
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
            Log.Information("Bootstrapping {JobDescription} job(s) in {AppMode} mode ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", jobOptions.Description, environment, options.InformationalVersion, Environment.MachineName, configDictionary, currentDirectory);

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
                    AddJobs(s, jobOptions);
                    
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
                    
                    app.UseHealthChecks("/health", new HealthCheckOptions {
                        Predicate = hcr => hcr.Tags.Contains("Core") || hcr.Tags.Contains(jobOptions.Description)
                    });

                    app.UseHealthChecks("/ready", new HealthCheckOptions {
                        Predicate = hcr => hcr.Tags.Contains("Core") || hcr.Tags.Contains(jobOptions.Description)
                    });

                    if (options.EnableBootstrapStartupActions)
                        app.UseStartupMiddleware();
                    
                    app.Use((context, func) => context.Response.WriteAsync($"Running Job: {jobOptions.Description}"));
                });
            
            if (useApplicationInsights)
                builder.UseApplicationInsights(options.ApplicationInsightsKey);

            var metricOptions = container.GetRequiredService<IOptions<MetricOptions>>().Value;
            if (!String.IsNullOrEmpty(metricOptions.Provider))
                ConfigureMetricsReporting(builder, metricOptions);

            return builder;
        }

        private static void AddJobs(IServiceCollection serviceCollection, JobRunnerOptions options) {
            if (options.CleanupSnapshot)
                serviceCollection.AddJob<CleanupSnapshotJob>(true);
            if (options.CloseInactiveSessions)
                serviceCollection.AddJob<CloseInactiveSessionsJob>(true);
            if (options.DailySummary)
                serviceCollection.AddJob<DailySummaryJob>(true);
            if (options.DownloadGeoipDatabase)
                serviceCollection.AddJob<DownloadGeoIPDatabaseJob>(true);
            if (options.EventNotifications)
                serviceCollection.AddJob<EventNotificationsJob>(true);
            if (options.EventPosts)
                serviceCollection.AddJob<EventPostsJob>(true);
            if (options.EventSnapshot)
                serviceCollection.AddJob<EventSnapshotJob>(true);
            if (options.EventUserDescriptions)
                serviceCollection.AddJob<EventUserDescriptionsJob>(true);
            if (options.MailMessage)
                serviceCollection.AddJob<MailMessageJob>(true);
            if (options.MaintainIndexes)
                serviceCollection.AddJob<MaintainIndexesJob>(true);
            if (options.OrganizationSnapshot)
                serviceCollection.AddJob<OrganizationSnapshotJob>(true);
            if (options.RetentionLimits)
                serviceCollection.AddJob<RetentionLimitsJob>(true);
            if (options.StackEventCount)
                serviceCollection.AddJob<StackEventCountJob>(true);
            if (options.StackSnapshot)
                serviceCollection.AddJob<StackSnapshotJob>(true);
            if (options.WebHooks)
                serviceCollection.AddJob<WebHooksJob>(true);
            if (options.WorkItem)
                serviceCollection.AddJob<WorkItemJob>(true);
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
