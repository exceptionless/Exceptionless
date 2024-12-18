using System.Diagnostics;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Exceptionless.Insulation.Configuration;
using Foundatio.Extensions.Hosting.Jobs;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Job;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            await CreateHostBuilder(args).Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Job host terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
            await ExceptionlessClient.Default.ProcessQueueAsync();

            if (Debugger.IsAttached)
                Console.ReadKey();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var jobOptions = new JobRunnerOptions(args);

        Console.Title = $"Exceptionless {jobOptions.JobName} Job";
        string environment = Environment.GetEnvironmentVariable("EX_AppMode") ?? "Production";
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
            .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
            .AddCustomEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateBootstrapLogger()
            .ForContext<Program>();

        var options = AppOptions.ReadFromConfiguration(config);
        // only poll the queue metrics if this process is going to run the stack event count job
        options.QueueOptions.MetricsPollingEnabled = jobOptions.StackEventCount;

        var apmConfig = new ApmConfig(config, $"job-{jobOptions.JobName.ToLowerUnderscoredWords('-')}", options.InformationalVersion, options.CacheOptions.Provider == "redis");

        Log.Information("Bootstrapping Exceptionless {JobName} job(s) in {AppMode} mode ({InformationalVersion}) on {MachineName} with options {@Options}", jobOptions.JobName ?? "All", environment, options.InformationalVersion, Environment.MachineName, options);

        var builder = Host.CreateDefaultBuilder()
            .UseEnvironment(environment)
            .ConfigureLogging(b => b.ClearProviders()) // clears .net providers since we are telling serilog to write to providers we only want it to be the otel provider
            .UseSerilog((ctx, sp, c) =>
            {
                c.ReadFrom.Configuration(ctx.Configuration);
                c.ReadFrom.Services(sp);
                c.Enrich.WithMachineName();

                if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                    c.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Information);
            }, writeToProviders: true)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseConfiguration(config)
                    .Configure(app =>
                    {
                        app.UseSerilogRequestLogging(o =>
                        {
                            o.MessageTemplate = "TraceId={TraceId} HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                            o.GetLevel = new Func<HttpContext, double, Exception?, LogEventLevel>((context, duration, ex) =>
                            {
                                if (ex is not null || context.Response.StatusCode > 499)
                                    return LogEventLevel.Error;

                                return duration < 1000 && context.Response.StatusCode < 400 ? LogEventLevel.Debug : LogEventLevel.Information;
                            });
                        });

                        Bootstrapper.LogConfiguration(app.ApplicationServices, options, app.ApplicationServices.GetRequiredService<ILogger<Program>>());

                        if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                            app.UseExceptionless(ExceptionlessClient.Default);

                        app.UseOpenTelemetryPrometheusScrapingEndpoint();

                        app.UseHealthChecks("/health", new HealthCheckOptions
                        {
                            Predicate = hcr => !String.IsNullOrEmpty(jobOptions.JobName) ? hcr.Tags.Contains(jobOptions.JobName) : hcr.Tags.Contains("AllJobs")
                        });

                        app.UseHealthChecks("/ready", new HealthCheckOptions
                        {
                            Predicate = hcr => hcr.Tags.Contains("Critical")
                        });

                        app.UseWaitForStartupActionsBeforeServingRequests();
                        app.Run(async context =>
                        {
                            await context.Response.WriteAsync($"Running Job: {jobOptions.JobName}");
                        });
                    });
            })
            .ConfigureServices((ctx, services) =>
            {
                AddJobs(services, jobOptions);
                services.AddAppOptions(options);

                Bootstrapper.RegisterServices(services, options);
                Insulation.Bootstrapper.RegisterServices(services, options, true);
            })
            .AddApm(apmConfig);

        return builder;
    }

    private static void AddJobs(IServiceCollection services, JobRunnerOptions options)
    {
        services.AddJobLifetimeService();

        if (options is { CleanupData: true, AllJobs: true })
            services.AddCronJob<CleanupDataJob>("30 */4 * * *");
        if (options is { CleanupData: true, AllJobs: false })
            services.AddJob<CleanupDataJob>();

        if (options is { CleanupOrphanedData: true, AllJobs: true })
            services.AddCronJob<CleanupOrphanedDataJob>("45 */8 * * *");
        if (options is { CleanupOrphanedData: true, AllJobs: false })
            services.AddJob<CleanupOrphanedDataJob>();

        if (options.CloseInactiveSessions)
            services.AddJob<CloseInactiveSessionsJob>(o => o.WaitForStartupActions());
        if (options.DailySummary)
            services.AddJob<DailySummaryJob>(o => o.WaitForStartupActions());
        if (options.DataMigration)
            services.AddJob<DataMigrationJob>(o => o.WaitForStartupActions());

        if (options is { DownloadGeoIPDatabase: true, AllJobs: true })
            services.AddCronJob<DownloadGeoIPDatabaseJob>("0 1 * * *");
        if (options is { DownloadGeoIPDatabase: true, AllJobs: false })
            services.AddJob<DownloadGeoIPDatabaseJob>(o => o.WaitForStartupActions());

        if (options.EventNotifications)
            services.AddJob<EventNotificationsJob>(o => o.WaitForStartupActions());
        if (options.EventPosts)
            services.AddJob<EventPostsJob>(o => o.WaitForStartupActions());
        if (options.EventUsage)
            services.AddJob<EventUsageJob>(o => o.WaitForStartupActions());
        if (options.EventUserDescriptions)
            services.AddJob<EventUserDescriptionsJob>(o => o.WaitForStartupActions());
        if (options.MailMessage)
            services.AddJob<MailMessageJob>(o => o.WaitForStartupActions());

        if (options is { MaintainIndexes: true, AllJobs: true })
            services.AddCronJob<MaintainIndexesJob>("10 */2 * * *");
        if (options is { MaintainIndexes: true, AllJobs: false })
            services.AddJob<MaintainIndexesJob>();

        if (options.Migration)
            services.AddJob<MigrationJob>(o => o.WaitForStartupActions());
        if (options.StackStatus)
            services.AddJob<StackStatusJob>(o => o.WaitForStartupActions());
        if (options.StackEventCount)
            services.AddJob<StackEventCountJob>(o => o.WaitForStartupActions());
        if (options.WebHooks)
            services.AddJob<WebHooksJob>(o => o.WaitForStartupActions());
        if (options.WorkItem)
            services.AddJob<WorkItemJob>(o => o.WaitForStartupActions());
    }
}
