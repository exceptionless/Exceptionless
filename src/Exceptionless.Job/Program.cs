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
using Foundatio.Hosting.Jobs;
using Foundatio.Hosting.Startup;
using Foundatio.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Job {
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
            var jobOptions = new JobRunnerOptions(args);
            
            Console.Title = jobOptions.JobName != null ? $"Exceptionless {jobOptions.JobName} Job" : "Exceptionless Jobs";
            
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

            var options = AppOptions.ReadFromConfiguration(config);
 
            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

            Log.Logger = loggerConfig.CreateLogger();
            var configDictionary = config.ToDictionary("Serilog");
            Log.Information("Bootstrapping Exceptionless {JobName} job(s) in {AppMode} mode ({InformationalVersion}) on {MachineName} with settings {@Settings}", jobOptions.JobName ?? "All", environment, options.InformationalVersion, Environment.MachineName, configDictionary);

            bool useApplicationInsights = !String.IsNullOrEmpty(options.ApplicationInsightsKey);

            var builder = Host.CreateDefaultBuilder()
                .UseEnvironment(environment)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder
                        .UseConfiguration(config)
                        .Configure(app => {
                            app.UseSerilogRequestLogging(o => o.GetLevel = (context, duration, ex) => {
                                if (ex != null || context.Response.StatusCode > 499)
                                    return LogEventLevel.Error;
                
                                return duration < 1000 && context.Response.StatusCode < 400 ? LogEventLevel.Debug : LogEventLevel.Information;
                            });    
                        })
                        .Configure(app => {
                            Bootstrapper.LogConfiguration(app.ApplicationServices, options, app.ApplicationServices.GetService<ILogger<Program>>());

                            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                                app.UseExceptionless(ExceptionlessClient.Default);
                    
                            app.UseHealthChecks("/health", new HealthCheckOptions {
                                Predicate = hcr => !String.IsNullOrEmpty(jobOptions.JobName) ? hcr.Tags.Contains(jobOptions.JobName) : hcr.Tags.Contains("AllJobs")
                            });

                            app.UseHealthChecks("/ready", new HealthCheckOptions {
                                Predicate = hcr => hcr.Tags.Contains("Critical")
                            });

                            app.UseWaitForStartupActionsBeforeServingRequests();
                            app.Use((context, func) => context.Response.WriteAsync($"Running Job: {jobOptions.JobName}"));
                        });

                    var metricOptions = MetricOptions.ReadFromConfiguration(config);
                    if (!String.IsNullOrEmpty(metricOptions.Provider))
                        ConfigureMetricsReporting(webBuilder, metricOptions);
                })
                .ConfigureServices((ctx, services) => {
                    AddJobs(services, jobOptions);
                    services.AddAppOptions(options);
                    
                    if (useApplicationInsights)
                        services.AddApplicationInsightsTelemetry(options.ApplicationInsightsKey);
                    
                    Bootstrapper.RegisterServices(services);
                    Insulation.Bootstrapper.RegisterServices(services, options, true);
                });

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
            if (options.DataMigration)
                services.AddJob<DataMigrationJob>(true);
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
            if (options.Migration)
                services.AddJob<MigrationJob>(true);
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
