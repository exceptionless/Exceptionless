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
            var jobOptions = new JobRunnerOptions(args);

            Console.Title = $"Exceptionless {jobOptions.JobName} Job";
            string? environment = Environment.GetEnvironmentVariable("EX_AppMode");
            if (String.IsNullOrWhiteSpace(environment))
                environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (String.IsNullOrWhiteSpace(environment))
                environment = Environments.Production;

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                EnvironmentName = environment
            });
            builder.Configuration.Sources.Clear();
            builder.Configuration
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
                .AddCustomEnvironmentVariables()
                .AddCommandLine(args);

            var configuration = (IConfigurationRoot)builder.Configuration;
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateBootstrapLogger()
                .ForContext<Program>();

            var options = AppOptions.ReadFromConfiguration(configuration);
            // only poll the queue metrics if this process is going to run the stack event count job
            options.QueueOptions.MetricsPollingEnabled = jobOptions.StackEventCount;

            var apmConfig = new ApmConfig(configuration, $"job-{jobOptions.JobName.ToLowerUnderscoredWords('-')}", options.InformationalVersion, options.CacheOptions.Provider == "redis");

            Log.Information("Bootstrapping Exceptionless {JobName} job(s) in {AppMode} mode ({InformationalVersion}) on {MachineName} with options {@Options}", jobOptions.JobName ?? "All", environment, options.InformationalVersion, Environment.MachineName, options);

            builder.Logging.ClearProviders();
            builder.Host
                .UseSerilog((ctx, sp, c) =>
                {
                    c.ReadFrom.Configuration(ctx.Configuration);
                    c.ReadFrom.Services(sp);
                    c.Enrich.WithMachineName();

                    if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                        c.WriteTo.Exceptionless(restrictedToMinimumLevel: LogEventLevel.Information);
                }, writeToProviders: true)
                .AddApm(apmConfig);

            AddJobs(builder.Services, jobOptions);
            builder.Services.AddAppOptions(options);
            Bootstrapper.RegisterServices(builder.Services, options);
            Insulation.Bootstrapper.RegisterServices(builder.Services, options, true);

            var app = builder.Build();

            app.UseSerilogRequestLogging(o =>
            {
                o.MessageTemplate = "TraceId={TraceId} HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                o.GetLevel = (context, duration, ex) =>
                {
                    if (ex is not null || context.Response.StatusCode > 499)
                        return LogEventLevel.Error;

                    return duration < 1000 && context.Response.StatusCode < 400 ? LogEventLevel.Debug : LogEventLevel.Information;
                };
            });

            Core.Bootstrapper.LogConfiguration(app.Services, options, app.Services.GetRequiredService<ILogger<Program>>());

            if (!String.IsNullOrEmpty(options.ExceptionlessApiKey) && !String.IsNullOrEmpty(options.ExceptionlessServerUrl))
                app.UseExceptionless(ExceptionlessClient.Default);

            app.UseHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => false
            });

            app.UseHealthChecks("/ready", new HealthCheckOptions
            {
                Predicate = hcr => hcr.Tags.Contains("Critical")
            });

            app.UseWaitForStartupActionsBeforeServingRequests();
            app.MapFallback(async context =>
            {
                await context.Response.WriteAsync($"Running Job: {jobOptions.JobName}");
            });

            await app.RunAsync();
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
