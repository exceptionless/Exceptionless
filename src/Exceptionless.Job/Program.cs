using System;
using System.IO;
using System.Threading.Tasks;
using Exceptionless.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Core.Jobs;
using Foundatio.Jobs;
using Exceptionless.Core.Jobs.Elastic;
using Fclp;

namespace Exceptionless.Job {
    public class Program {
        public static async Task<int> Main(string[] args) {
            try {
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

                Settings.Initialize(config, environment);
                Settings.Current.DisableIndexConfiguration = true;

                var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(config);
                if (!String.IsNullOrEmpty(Settings.Current.ExceptionlessApiKey))
                    loggerConfig.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Verbose);

                Log.Logger = loggerConfig.CreateLogger();

                Log.Information("Bootstrapping {AppMode} mode job ({InformationalVersion}) on {MachineName} using {@Settings} loaded from {Folder}", environment, Settings.Current.InformationalVersion, Environment.MachineName, Settings.Current, currentDirectory);

                var parser = JobRunnerArguments.GetParser();
                var result = parser.Parse(args);
                if (result.HasErrors)
                    return 1;

                var jobArguments = parser.Object;
                jobArguments.RunAllByDefault();

                var builder = new HostBuilder()
                    .UseEnvironment(environment)
                    .ConfigureAppConfiguration(c => c.AddConfiguration(config))
                    .ConfigureLogging(b => b.AddSerilog(Log.Logger))
                    .ConfigureServices(s => {
                        Bootstrapper.RegisterServices(s);
                        Insulation.Bootstrapper.RegisterServices(s, true);

                        if (jobArguments.EventPostsJob == true)
                            s.AddJob<EventPostsJob>();

                        if (jobArguments.EventUserDescriptionsJob == true)
                            s.AddJob<EventUserDescriptionsJob>();

                        if (jobArguments.EventNotificationsJob == true)
                            s.AddJob<EventNotificationsJob>();

                        if (jobArguments.MailMessageJob == true)
                            s.AddJob<MailMessageJob>();

                        if (jobArguments.WebHooksJob == true)
                            s.AddJob<WebHooksJob>();

                        if (jobArguments.CloseInactiveSessionsJob == true)
                            s.AddJob<CloseInactiveSessionsJob>();

                        if (jobArguments.DailySummaryJob == true)
                            s.AddJob<DailySummaryJob>();

                        if (jobArguments.DownloadGeoIPDatabaseJob == true)
                            s.AddJob<DownloadGeoIPDatabaseJob>();

                        if (jobArguments.RetentionLimitsJob == true)
                            s.AddJob<RetentionLimitsJob>();

                        if (jobArguments.WorkItemJob == true)
                            s.AddJob<WorkItemJob>();

                        if (jobArguments.MaintainIndexesJob == true)
                            s.AddJob<MaintainIndexesJob>();

                        if (jobArguments.StackEventCountJob == true)
                            s.AddJob<StackEventCountJob>();
                    });

                await builder.RunConsoleAsync();
                return 0;
            } catch (Exception ex) {
                Log.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                await ExceptionlessClient.Default.ProcessQueueAsync();
            }
        }
    }

    public class JobRunnerArguments {
        public bool EventPostsJob { get; set; }
        public bool EventUserDescriptionsJob { get; set; }
        public bool EventNotificationsJob { get; set; }
        public bool MailMessageJob { get; set; }
        public bool WebHooksJob { get; set; }
        public bool CloseInactiveSessionsJob { get; set; }
        public bool DailySummaryJob { get; set; }
        public bool DownloadGeoIPDatabaseJob { get; set; }
        public bool RetentionLimitsJob { get; set; }
        public bool WorkItemJob { get; set; }
        public bool MaintainIndexesJob { get; set; }
        public bool StackEventCountJob { get; set; }

        public static FluentCommandLineParser<JobRunnerArguments> GetParser() {
            var p = new FluentCommandLineParser<JobRunnerArguments>();

            p.Setup(arg => arg.EventPostsJob)
                .As('e', "event-posts")
                .WithDescription("Wether to run the EventPostsJob");
            p.Setup(arg => arg.EventUserDescriptionsJob)
                .As('u', "event-user-descriptions")
                .WithDescription("Wether to run the EventUserDescriptionsJob");
            p.Setup(arg => arg.EventNotificationsJob)
                .As('n', "event-notifications")
                .WithDescription("Wether to run the EventNotificationsJob");
            p.Setup(arg => arg.MailMessageJob)
                .As('m', "mail-message")
                .WithDescription("Wether to run the MailMessageJob");
            p.Setup(arg => arg.WebHooksJob)
                .As('h', "web-hooks")
                .WithDescription("Wether to run the WebHooksJob");
            p.Setup(arg => arg.CloseInactiveSessionsJob)
                .As('s', "close-inactive-sessions")
                .WithDescription("Wether to run the CloseInactiveSessionsJob");
            p.Setup(arg => arg.DailySummaryJob)
                .As('d', "daily-summary")
                .WithDescription("Wether to run the DailySummaryJob");
            p.Setup(arg => arg.DownloadGeoIPDatabaseJob)
                .As('g', "download-geoip-database")
                .WithDescription("Wether to run the DownloadGeoIPDatabaseJob");
            p.Setup(arg => arg.RetentionLimitsJob)
                .As('r', "retention-limits")
                .WithDescription("Wether to run the RetentionLimitsJob");
            p.Setup(arg => arg.WorkItemJob)
                .As('w', "work-item")
                .WithDescription("Wether to run the WorkItemJob");
            p.Setup(arg => arg.MaintainIndexesJob)
                .As('i', "maintain-indexes")
                .WithDescription("Wether to run the MaintainIndexesJob");
            p.Setup(arg => arg.StackEventCountJob)
                .As('c', "stack-event-count")
                .WithDescription("Wether to run the StackEventCountJob");

            return p;
        }

        public void RunAllByDefault() {
            if (EventPostsJob
                || EventUserDescriptionsJob
                || EventNotificationsJob
                || MailMessageJob
                || WebHooksJob
                || CloseInactiveSessionsJob
                || DailySummaryJob
                || DownloadGeoIPDatabaseJob
                || RetentionLimitsJob
                || WorkItemJob
                || MaintainIndexesJob
                || StackEventCountJob)
                return;

            EventPostsJob = true;
            EventUserDescriptionsJob = true;
            EventNotificationsJob = true;
            MailMessageJob = true;
            WebHooksJob = true;
            CloseInactiveSessionsJob = true;
            DailySummaryJob = true;
            DownloadGeoIPDatabaseJob = true;
            RetentionLimitsJob = true;
            WorkItemJob = true;
            MaintainIndexesJob = true;
            StackEventCountJob = true;
        }
    }
}
