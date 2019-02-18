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
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Exceptionless.Insulation.Configuration;
using Foundatio.Hosting;
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
using Serilog.AspNetCore;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Job {
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
            var jobOptions = new JobRunnerOptions(args);
            string environment = Environment.GetEnvironmentVariable("EX_AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            Console.Title = jobOptions.JobName != null ? $"Exceptionless {jobOptions.JobName} Job" : "Exceptionless Jobs";

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
            _logger.LogInformation("Bootstrapping Exceptionless {JobName} job(s) in {AppMode} mode ({InformationalVersion}) on {MachineName} with settings {@Settings}", jobOptions.JobName ?? "All", environment, options.InformationalVersion, Environment.MachineName, configDictionary, currentDirectory);

            bool useApplicationInsights = !String.IsNullOrEmpty(options.ApplicationInsightsKey);

            var builder = WebHost.CreateDefaultBuilder(args)
                .UseEnvironment(environment)
                .ConfigureKestrel(c => {
                    c.AddServerHeader = false;
                    c.AllowSynchronousIO = false;
                })
                .UseConfiguration(config)
                .ConfigureServices(s => {
                    s.AddSingleton<ILoggerFactory>(loggerFactory);
                    s.AddHttpContextAccessor();
                    
                    AddJobs(s, jobOptions);
                    
                    if (useApplicationInsights)
                        s.AddApplicationInsightsTelemetry();
                    
                    Bootstrapper.RegisterServices(s);
                    var serviceProvider = s.BuildServiceProvider();
                    Insulation.Bootstrapper.RegisterServices(serviceProvider, s, options, true);
                })
                .Configure(app => {
                    Bootstrapper.LogConfiguration(app.ApplicationServices, options, loggerFactory);

                    if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                        app.UseExceptionless(ExceptionlessClient.Default);
                    
                    app.UseHealthChecks("/health", new HealthCheckOptions {
                        Predicate = hcr => hcr.Tags.Contains("Liveness") || hcr.Tags.Contains(jobOptions.JobName)
                    });

                    app.UseHealthChecks("/ready", new HealthCheckOptions {
                        Predicate = hcr => hcr.Tags.Contains("Critical") || hcr.Tags.Contains(jobOptions.JobName)
                    });

                    if (options.EnableBootstrapStartupActions)
                        app.UseWaitForStartupActionsBeforeServingRequests();
                    
                    app.Use((context, func) => context.Response.WriteAsync($"Running Job: {jobOptions.JobName}"));
                });
            
            if (useApplicationInsights)
                builder.UseApplicationInsights(options.ApplicationInsightsKey);

            var metricOptions = container.GetRequiredService<IOptions<MetricOptions>>().Value;
            if (!String.IsNullOrEmpty(metricOptions.Provider))
                ConfigureMetricsReporting(builder, metricOptions);

            return builder;
        }

        private static void AddJobs(IServiceCollection services, JobRunnerOptions options) {
            services.AddJobLifetimeService();
            
            if (options.CleanupSnapshot)
                services.AddJob<CleanupSnapshotJob>(true);
            if (options.CloseInactiveSessions)
                services.AddJob<CloseInactiveSessionsJob>(true);
            if (options.DailySummary)
                services.AddJob<DailySummaryJob>(true);
            if (options.DownloadGeoipDatabase)
                services.AddJob<DownloadGeoIPDatabaseJob>(true);
            if (options.EventNotifications)
                services.AddJob<EventNotificationsJob>(true);
            if (options.EventPosts)
                services.AddJob<EventPostsJob>(true);
            if (options.EventSnapshot)
                services.AddJob<EventSnapshotJob>(true);
            if (options.EventUserDescriptions)
                services.AddJob<EventUserDescriptionsJob>(true);
            if (options.MailMessage)
                services.AddJob<MailMessageJob>(true);
            if (options.MaintainIndexes)
                services.AddCronJob<MaintainIndexesJob>("10 */2 * * *");
            if (options.OrganizationSnapshot)
                services.AddJob<OrganizationSnapshotJob>(true);
            if (options.RetentionLimits)
                services.AddJob<RetentionLimitsJob>(true);
            if (options.StackEventCount)
                services.AddJob<StackEventCountJob>(true);
            if (options.StackSnapshot)
                services.AddJob<StackSnapshotJob>(true);
            if (options.WebHooks)
                services.AddJob<WebHooksJob>(true);
            if (options.WorkItem)
                services.AddJob<WorkItemJob>(true);
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
