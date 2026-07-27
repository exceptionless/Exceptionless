using Exceptionless.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests;

public class BootstrapperTests
{
    [Fact]
    public void LogConfiguration_ConfiguredServices_LogsCuratedSafeSummary()
    {
        var options = CreateConfiguredOptions();
        var logger = new CollectingLogger(LogLevel.Information);
        using var serviceProvider = CreateServiceProvider("Integration");

        Bootstrapper.LogConfiguration(serviceProvider, options, logger);

        string[] informationMessages = logger.Entries
            .Where(entry => entry.Level == LogLevel.Information)
            .Select(entry => entry.Message)
            .ToArray();

        Assert.Equal(
        [
            "Startup configuration: environment Integration, scope tenant-a, mode Staging, version 1.2.3+build.4, base URL https://app.example.test:8443",
            "Startup infrastructure: Elasticsearch https://elastic.example.test:9200 (migration https://elastic-migrate.example.test:9243, shards 3, replicas 2, index configuration enabled, snapshot jobs enabled); cache redis at cache.example.test:6380; message bus rabbitmq at amqps://rabbit.example.test:5671 (topic tenant-a-messages); queue sqs at https://queue.example.test (region us-east-2); storage azurestorage at https://visibleaccount.blob.core.windows.net (region us-central-1)",
            "Startup services: event submission enabled; web sockets enabled; jobs in process enabled; repository notifications enabled; archive enabled; sample data disabled; email enabled at smtp.example.test:465 (SSL, daily summaries enabled); account creation enabled; Active Directory enabled at ldap.example.test:389",
            "Startup integrations: OAuth Google, Microsoft, GitHub; Intercom enabled; Slack enabled; billing enabled; geocoding enabled; GeoIP enabled; internal Exceptionless logging enabled at https://collector.example.test",
            "Startup operations: retention 365 days; maximum event post 250000 bytes; bulk batch 750; API throttle 5000; bot throttle 30; queue metrics polling enabled every 00:00:07; disabled pipeline actions 1; disabled plugins 2",
            "Startup source maps: auto-download enabled; request timeout 00:00:04; processing timeout 00:00:06; per-project artifacts 200 / storage 524288000 bytes; free retention 10 days; paid retention 60 days; download concurrency 3 local / 12 global"
        ],
        informationMessages);

        string output = String.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message));
        Assert.Contains("cache.example.test:6380", output, StringComparison.Ordinal);
        Assert.Contains("rabbit.example.test:5671", output, StringComparison.Ordinal);
        Assert.Contains("us-east-2", output, StringComparison.Ordinal);
        Assert.Contains("smtp.example.test:465", output, StringComparison.Ordinal);
        Assert.Contains("elastic.example.test:9200", output, StringComparison.Ordinal);
        Assert.Contains("retention 365 days", output, StringComparison.Ordinal);

        foreach (string secret in GetCanarySecrets())
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
    }

    [Fact]
    public void LogConfiguration_DisabledServices_LogsDisabledSummaryAndPreservesWarnings()
    {
        var options = CreateOptions(
        [
            new("BaseURL", "http://localhost:5000"),
            new("AppMode", "Production"),
            new("AppScope", "disabled-test"),
            new("EnableWebSockets", "false"),
            new("EnableAccountCreation", "false"),
            new("EventSubmissionDisabled", "true"),
            new("SourceMaps:EnableAutoDownload", "false")
        ]);
        var logger = new CollectingLogger(LogLevel.Information);
        using var serviceProvider = CreateServiceProvider("Production");

        Bootstrapper.LogConfiguration(serviceProvider, options, logger);

        string output = String.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message));
        Assert.Contains("cache disabled at not configured", output, StringComparison.Ordinal);
        Assert.Contains("message bus disabled at not configured", output, StringComparison.Ordinal);
        Assert.Contains("queue disabled at not configured", output, StringComparison.Ordinal);
        Assert.Contains("storage disabled at not configured", output, StringComparison.Ordinal);
        Assert.Contains("event submission disabled", output, StringComparison.Ordinal);
        Assert.Contains("web sockets disabled", output, StringComparison.Ordinal);
        Assert.Contains("email disabled at not configured", output, StringComparison.Ordinal);
        Assert.Contains("account creation disabled", output, StringComparison.Ordinal);
        Assert.Contains("OAuth disabled", output, StringComparison.Ordinal);
        Assert.Contains("auto-download disabled", output, StringComparison.Ordinal);

        string[] warnings = logger.Entries
            .Where(entry => entry.Level == LogLevel.Warning)
            .Select(entry => entry.Message)
            .ToArray();
        Assert.Contains(warnings, message => message.StartsWith("Distributed cache is NOT enabled", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Distributed message bus is NOT enabled", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Distributed queue is NOT enabled", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Distributed storage is NOT enabled", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Web Sockets is NOT enabled", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Emails will NOT be sent", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Event Submission is NOT enabled", StringComparison.Ordinal));
        Assert.Contains(warnings, message => message.StartsWith("Account Creation is NOT enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void LogConfiguration_InformationDisabled_DoesNotLogSummaryAndPreservesWarnings()
    {
        var options = CreateOptions(
        [
            new("BaseURL", "http://localhost:5000"),
            new("EnableWebSockets", "false")
        ]);
        var logger = new CollectingLogger(LogLevel.Warning);
        using var serviceProvider = CreateServiceProvider("Production");

        Bootstrapper.LogConfiguration(serviceProvider, options, logger);

        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.StartsWith("Web Sockets is NOT enabled", StringComparison.Ordinal));
    }

    private static AppOptions CreateConfiguredOptions()
    {
        var options = CreateOptions(
        [
            new("BaseURL", "https://base-user:base-password-secret-canary@app.example.test:8443/path?token=query-token-secret-canary"),
            new("ExceptionlessApiKey", "exceptionless-api-key-secret-canary"),
            new("ExceptionlessServerUrl", "https://collector-user:collector-password-secret-canary@collector.example.test/path?api_key=collector-query-secret-canary"),
            new("AppMode", "Staging"),
            new("AppScope", "tenant-a"),
            new("RunJobsInProcess", "true"),
            new("JobsIterationLimit", "42"),
            new("BotThrottleLimit", "30"),
            new("ApiThrottleLimit", "5000"),
            new("EnableArchive", "true"),
            new("EnableSampleData", "false"),
            new("MaximumEventPostSize", "250000"),
            new("MaximumRetentionDays", "365"),
            new("BulkBatchSize", "750"),
            new("EnableRepositoryNotifications", "true"),
            new("EnableWebSockets", "true"),
            new("DisabledPipelineActions", "CanaryPipelineAction"),
            new("DisabledPlugins", "CanaryPlugin1,CanaryPlugin2"),
            new("GoogleGeocodingApiKey", "google-api-key-secret-canary"),
            new("MaxMindGeoIpKey", "maxmind-key-secret-canary"),
            new("EnableDailySummary", "true"),
            new("EnableAccountCreation", "true"),
            new("EnableActiveDirectoryAuth", "true"),
            new("ConnectionStrings:Cache", "provider=redis;server=\"cache.example.test:6380,password=cache-password-secret-canary,abortConnect=false\""),
            new("ConnectionStrings:MessageBus", "provider=rabbitmq;server=\"amqps://rabbit-user:rabbit-password-secret-canary@rabbit.example.test:5671/vhost\""),
            new("ConnectionStrings:Queue", "provider=sqs;serviceurl=\"https://queue-user:queue-password-secret-canary@queue.example.test/v1?token=queue-token-secret-canary\";region=us-east-2;accesskey=queue-access-key-secret-canary;secretkey=queue-secret-key-secret-canary"),
            new("ConnectionStrings:Storage", "provider=azurestorage;BlobEndpoint=\"https://storage-user:storage-password-secret-canary@visibleaccount.blob.core.windows.net/container?sig=storage-signature-secret-canary\";AccountName=visibleaccount;AccountKey=storage-account-key-secret-canary;region=us-central-1"),
            new("ConnectionStrings:Elasticsearch", "server=\"https://elastic-user:elastic-password-secret-canary@elastic.example.test:9200/path?token=elastic-token-secret-canary\";shards=3;replicas=2"),
            new("ConnectionStrings:ElasticsearchToMigrate", "server=\"https://migrate-user:migrate-password-secret-canary@elastic-migrate.example.test:9243\""),
            new("ConnectionStrings:Email", "smtps://smtp-user:smtp-password-secret-canary@smtp.example.test:465"),
            new("ConnectionStrings:LDAP", "server=ldap.example.test;port=389;username=ldap-user;password=ldap-password-secret-canary"),
            new("ConnectionStrings:OAuth", "GoogleId=google-id;GoogleSecret=google-secret-canary;MicrosoftId=microsoft-id;MicrosoftSecret=microsoft-secret-canary;GitHubId=github-id;GitHubSecret=github-secret-canary;IntercomId=intercom-id;IntercomSecret=intercom-secret-canary;SlackId=slack-id;SlackSecret=slack-secret-canary"),
            new("StripeApiKey", "stripe-api-key-secret-canary"),
            new("StripePublishableApiKey", "stripe-publishable-key"),
            new("StripeWebHookSigningSecret", "stripe-signing-secret-canary"),
            new("EnableSnapshotJobs", "true"),
            new("QueueMetricsPollingEnabled", "true"),
            new("SourceMaps:EnableAutoDownload", "true"),
            new("SourceMaps:RequestTimeoutMilliseconds", "4000"),
            new("SourceMaps:MaximumProcessingTimeMilliseconds", "6000"),
            new("SourceMaps:MaximumArtifactsPerProject", "200"),
            new("SourceMaps:MaximumStorageSizePerProject", "524288000"),
            new("SourceMaps:FreeArtifactRetentionDays", "10"),
            new("SourceMaps:ArtifactRetentionDays", "60"),
            new("SourceMaps:MaximumConcurrentDownloads", "3"),
            new("SourceMaps:MaximumConcurrentDownloadsGlobally", "12")
        ]);

        options.Version = "1.2.3";
        options.InformationalVersion = "1.2.3+build.4";
        options.QueueOptions.MetricsPollingEnabled = true;
        options.QueueOptions.MetricsPollingInterval = TimeSpan.FromSeconds(7);
        return options;
    }

    private static AppOptions CreateOptions(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return AppOptions.ReadFromConfiguration(configuration);
    }

    private static ServiceProvider CreateServiceProvider(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment
        {
            EnvironmentName = environmentName
        });
        return services.BuildServiceProvider();
    }

    private static string[] GetCanarySecrets()
    {
        return
        [
            "base-user",
            "base-password-secret-canary",
            "query-token-secret-canary",
            "exceptionless-api-key-secret-canary",
            "collector-user",
            "collector-password-secret-canary",
            "collector-query-secret-canary",
            "cache-password-secret-canary",
            "rabbit-user",
            "rabbit-password-secret-canary",
            "queue-user",
            "queue-password-secret-canary",
            "queue-token-secret-canary",
            "queue-access-key-secret-canary",
            "queue-secret-key-secret-canary",
            "storage-user",
            "storage-password-secret-canary",
            "storage-signature-secret-canary",
            "storage-account-key-secret-canary",
            "elastic-user",
            "elastic-password-secret-canary",
            "elastic-token-secret-canary",
            "migrate-user",
            "migrate-password-secret-canary",
            "smtp-user",
            "smtp-password-secret-canary",
            "ldap-user",
            "ldap-password-secret-canary",
            "google-secret-canary",
            "microsoft-secret-canary",
            "github-secret-canary",
            "intercom-secret-canary",
            "slack-secret-canary",
            "stripe-api-key-secret-canary",
            "stripe-signing-secret-canary"
        ];
    }

    private sealed class CollectingLogger(LogLevel minimumLevel) : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Exceptionless.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
