using System.Reflection;
using Exceptionless;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Insulation.Security;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;
using Xunit;

namespace Exceptionless.Tests.Logging;

public class SensitiveDataLoggingTests
{
    [Fact]
    public void ApplySensitiveDataLogging_AppOptions_PreservesSafeValuesAndRedactsNestedSecrets()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var options = CreateAppOptions();

        logger.Information("Loaded configuration {@Options}", options);

        var structure = Assert.IsType<StructureValue>(sink.Events.Single().Properties["Options"]);
        string rendered = structure.ToString();
        Assert.Contains("AppOptions", rendered, StringComparison.Ordinal);
        Assert.Contains("https://app.example.test", rendered, StringComparison.Ordinal);
        Assert.Contains("internal-project-id", rendered, StringComparison.Ordinal);
        Assert.Contains("smtp.example.test", rendered, StringComparison.Ordinal);
        Assert.Contains("smtp-user", rendered, StringComparison.Ordinal);
        Assert.Contains("VisiblePipelineAction", rendered, StringComparison.Ordinal);
        Assert.Contains("VisiblePlugin", rendered, StringComparison.Ordinal);
        Assert.Contains("redis.example.test:6379", rendered, StringComparison.Ordinal);
        Assert.Contains("abortConnect=False", rendered, StringComparison.Ordinal);
        Assert.Contains("elastic.example.test:9200", rendered, StringComparison.Ordinal);
        Assert.Contains("elastic-migrate.example.test:9200", rendered, StringComparison.Ordinal);
        Assert.Contains("rabbit.example.test", rendered, StringComparison.Ordinal);
        Assert.Contains("rabbit-user", rendered, StringComparison.Ordinal);
        Assert.Contains("AKIA_VISIBLE_ACCESS_KEY", rendered, StringComparison.Ordinal);
        Assert.Contains("us-east-2", rendered, StringComparison.Ordinal);
        Assert.Contains("visibleaccount", rendered, StringComparison.Ordinal);
        Assert.Contains("core.windows.net", rendered, StringComparison.Ordinal);
        Assert.Contains("pk_publishable-visible", rendered, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", rendered, StringComparison.Ordinal);

        foreach (string secret in GetAppOptionCanarySecrets())
            Assert.DoesNotContain(secret, rendered, StringComparison.Ordinal);

        object[] originalOptionObjects =
        [
            options,
            options.CacheOptions,
            options.MessageBusOptions,
            options.QueueOptions,
            options.StorageOptions,
            options.EmailOptions,
            options.ElasticsearchOptions,
            options.ElasticsearchOptions.ElasticsearchToMigrate!,
            options.IntercomOptions,
            options.SlackOptions,
            options.StripeOptions,
            options.AuthOptions,
            options.OAuthServerOptions,
            options.SourceMapOptions
        ];
        Assert.DoesNotContain(
            EnumerateScalarValues(structure),
            value => originalOptionObjects.Any(original => ReferenceEquals(value, original)));
    }

    [Fact]
    public void ApplySensitiveDataLogging_SensitiveOptionTypes_IncludeEveryPublicProperty()
    {
        object[] options =
        [
            new AppOptions(),
            new AuthOptions(),
            new CacheOptions(),
            new ElasticsearchOptions(),
            new EmailOptions(),
            new IntercomOptions(),
            new MessageBusOptions(),
            new MetricOptions(),
            new QueueOptions(),
            new StorageOptions(),
            new StripeOptions(),
            new SlackOptions(),
            new OAuthServerOptions(),
            new SourceMapOptions()
        ];

        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(sink)
            .CreateLogger();

        foreach (object option in options)
            logger.Information("Loaded configuration {@Options}", option);

        Assert.Equal(options.Length, sink.Events.Count);
        for (int index = 0; index < options.Length; index++)
        {
            string[] expectedProperties = options[index]
                .GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetMethod?.IsPrivate is false && property.GetIndexParameters().Length == 0)
                .Select(property => property.Name)
                .Order()
                .ToArray();
            var propertyValue = sink.Events[index].Properties["Options"];
            if (propertyValue is not StructureValue)
                Assert.Fail($"{options[index].GetType().Name} was captured as {propertyValue.GetType().Name}.");

            var structure = (StructureValue)propertyValue;
            string[] actualProperties = structure.Properties
                .Select(property => property.Name)
                .Order()
                .ToArray();

            Assert.Equal(expectedProperties, actualProperties);
        }
    }

    [Fact]
    public void ApplySensitiveDataLogging_MetricOptions_PreservesSafeConnectionStringComponents()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var options = CreateMetricOptions();

        logger.Information("Loaded configuration {@Options}", options);

        var structure = Assert.IsType<StructureValue>(sink.Events.Single().Properties["Options"]);
        string rendered = structure.ToString();
        Assert.Contains("MetricOptions", rendered, StringComparison.Ordinal);
        Assert.Contains("statsd", rendered, StringComparison.Ordinal);
        Assert.Contains("metrics.example.test", rendered, StringComparison.Ordinal);
        Assert.Contains("8125", rendered, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("metric-password-secret-canary", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(EnumerateScalarValues(structure), value => ReferenceEquals(value, options));
    }

    [Fact]
    public void ApplySensitiveDataLogging_ExceptionlessSink_SerializedPayloadPreservesSafeValuesAndOmitsCanarySecrets()
    {
        Event? submittedEvent = null;
        var appOptions = CreateAppOptions();
        var metricOptions = CreateMetricOptions();

        using var client = new ExceptionlessClient(configuration =>
        {
            configuration.ApiKey = "00000000000000000000000000000000";
            configuration.UseInMemoryStorage();
        });
        client.SubmittingEvent += (_, args) =>
        {
            submittedEvent = args.Event;
            args.Cancel = true;
        };

        using (var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(new ExceptionlessSink(client: client))
            .CreateLogger())
        {
            logger.Information(
                "Loaded configuration {@AppOptions} {@MetricOptions}",
                appOptions,
                metricOptions);
        }

        Assert.NotNull(submittedEvent);
        string payload = new DefaultJsonSerializer().Serialize(submittedEvent);

        Assert.Contains("https://app.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("internal-project-id", payload, StringComparison.Ordinal);
        Assert.Contains("smtp.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("VisiblePipelineAction", payload, StringComparison.Ordinal);
        Assert.Contains("VisiblePlugin", payload, StringComparison.Ordinal);
        Assert.Contains("redis.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("elastic.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("elastic-migrate.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("rabbit.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("rabbit-user", payload, StringComparison.Ordinal);
        Assert.Contains("AKIA_VISIBLE_ACCESS_KEY", payload, StringComparison.Ordinal);
        Assert.Contains("us-east-2", payload, StringComparison.Ordinal);
        Assert.Contains("visibleaccount", payload, StringComparison.Ordinal);
        Assert.Contains("core.windows.net", payload, StringComparison.Ordinal);
        Assert.Contains("pk_publishable-visible", payload, StringComparison.Ordinal);
        Assert.Contains("metrics.example.test", payload, StringComparison.Ordinal);
        Assert.Contains("8125", payload, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", payload, StringComparison.Ordinal);

        foreach (string secret in GetAppOptionCanarySecrets().Append("metric-password-secret-canary"))
            Assert.DoesNotContain(secret, payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySensitiveDataLogging_OrdinaryNonSensitiveObject_DoesNotTraverse()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var value = new TraversalTrackingValue();

        logger.Information("Loaded value {Value}", value);

        Assert.Equal(0, value.ValueAccessCount);
        var scalar = Assert.IsType<ScalarValue>(sink.Events.Single().Properties["Value"]);
        Assert.IsType<string>(scalar.Value);
        Assert.NotSame(value, scalar.Value);
    }

    private static AppOptions CreateAppOptions()
    {
        return new AppOptions
        {
            BaseURL = "https://app.example.test",
            InternalProjectId = "internal-project-id",
            ExceptionlessApiKey = "exceptionless-api-key-secret-canary",
            ExceptionlessServerUrl = "https://collector.example.test",
            AppMode = AppMode.Staging,
            AppScope = "visible-scope",
            RunJobsInProcess = true,
            JobsIterationLimit = 42,
            BotThrottleLimit = 43,
            ApiThrottleLimit = 44,
            EnableArchive = true,
            EnableSampleData = false,
            EventSubmissionDisabled = false,
            DisabledPipelineActions = ["VisiblePipelineAction"],
            DisabledPlugins = ["VisiblePlugin"],
            MaximumEventPostSize = 200_000,
            MaximumRetentionDays = 180,
            EnableRepositoryNotifications = true,
            EnablePush = true,
            Version = "1.2.3",
            InformationalVersion = "1.2.3+visible",
            NotificationMessage = "visible notification",
            GoogleGeocodingApiKey = "google-api-key-secret-canary",
            MaxMindGeoIpKey = "maxmind-key-secret-canary",
            BulkBatchSize = 1_000,
            CacheOptions = new CacheOptions
            {
                Provider = "redis",
                ConnectionString = "redis.example.test:6379,password=redis-password-secret-canary,abortConnect=false",
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["provider"] = "redis",
                    ["server"] = "redis.example.test:6379,password=redis-data-password-secret-canary,abortConnect=false"
                },
                Scope = "visible-scope",
                ScopePrefix = "visible-scope-"
            },
            EmailOptions = new EmailOptions
            {
                EnableDailySummary = true,
                AllowedOutboundAddresses = ["example.test"],
                SmtpHost = "smtp.example.test",
                SmtpPort = 587,
                SmtpEncryption = SmtpEncryption.StartTLS,
                SmtpUser = "smtp-user",
                SmtpPassword = "smtp-password-secret-canary"
            },
            ElasticsearchOptions = new ElasticsearchOptions
            {
                ServerUrl = "https://elastic-user:elastic-uri-password-secret-canary@elastic.example.test:9200",
                UserName = "elastic-user",
                Password = "elastic-password-secret-canary",
                NumberOfShards = 3,
                ElasticsearchToMigrate = new ElasticsearchOptions
                {
                    ServerUrl = "https://migrate-user:elastic-migrate-uri-password-secret-canary@elastic-migrate.example.test:9200",
                    UserName = "migrate-user",
                    Password = "elastic-migrate-password-secret-canary"
                }
            },
            StripeOptions = new StripeOptions
            {
                StripeApiKey = "stripe-api-key-secret-canary",
                StripePublishableApiKey = "pk_publishable-visible",
                StripeWebHookSigningSecret = "stripe-signing-secret-canary"
            },
            AuthOptions = new AuthOptions
            {
                EnableAccountCreation = true,
                MicrosoftId = "microsoft-id-visible",
                MicrosoftSecret = "microsoft-secret-canary",
                LdapConnectionString = "server=ldap.example.test;password=ldap-password-secret-canary"
            },
            IntercomOptions = new IntercomOptions
            {
                IntercomId = "intercom-id-visible",
                IntercomSecret = "intercom-secret-canary"
            },
            SlackOptions = new SlackOptions
            {
                SlackId = "slack-id-visible",
                SlackSecret = "slack-secret-canary"
            },
            MessageBusOptions = new MessageBusOptions
            {
                Provider = "rabbitmq",
                ConnectionString = "server=amqp://rabbit-user:rabbit-password-secret-canary@rabbit.example.test:5672/vhost",
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["provider"] = "rabbitmq",
                    ["server"] = "amqp://rabbit-user:rabbit-data-password-secret-canary@rabbit.example.test:5672/vhost"
                },
                Scope = "visible-scope",
                ScopePrefix = "visible-scope-",
                Topic = "visible-topic"
            },
            QueueOptions = new QueueOptions
            {
                Provider = "sqs",
                ConnectionString = "accesskey=AKIA_VISIBLE_ACCESS_KEY;secretkey=aws-secret-key-secret-canary;region=us-east-2",
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["provider"] = "sqs",
                    ["accesskey"] = "AKIA_VISIBLE_ACCESS_KEY",
                    ["secretkey"] = "aws-data-secret-key-secret-canary",
                    ["region"] = "us-east-2"
                },
                Scope = "visible-scope",
                ScopePrefix = "visible-scope-"
            },
            StorageOptions = new StorageOptions
            {
                Provider = "azurestorage",
                ConnectionString = "DefaultEndpointsProtocol=https;AccountName=visibleaccount;AccountKey=azure-account-key-secret-canary;EndpointSuffix=core.windows.net",
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["provider"] = "azurestorage",
                    ["DefaultEndpointsProtocol"] = "https",
                    ["AccountName"] = "visibleaccount",
                    ["AccountKey"] = "azure-data-account-key-secret-canary",
                    ["EndpointSuffix"] = "core.windows.net"
                },
                Scope = "visible-scope",
                ScopePrefix = "visible-scope-"
            },
            OAuthServerOptions = new OAuthServerOptions(),
            SourceMapOptions = new SourceMapOptions()
        };
    }

    private static MetricOptions CreateMetricOptions()
    {
        return new MetricOptions
        {
            Provider = "statsd",
            ConnectionString = "server=metrics.example.test;port=8125;password=metric-password-secret-canary",
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = "statsd",
                ["server"] = "metrics.example.test",
                ["port"] = "8125",
                ["password"] = "metric-password-secret-canary"
            }
        };
    }

    private static string[] GetAppOptionCanarySecrets()
    {
        return
        [
            "exceptionless-api-key-secret-canary",
            "google-api-key-secret-canary",
            "maxmind-key-secret-canary",
            "redis-password-secret-canary",
            "redis-data-password-secret-canary",
            "smtp-password-secret-canary",
            "elastic-uri-password-secret-canary",
            "elastic-password-secret-canary",
            "elastic-migrate-uri-password-secret-canary",
            "elastic-migrate-password-secret-canary",
            "rabbit-password-secret-canary",
            "rabbit-data-password-secret-canary",
            "aws-secret-key-secret-canary",
            "aws-data-secret-key-secret-canary",
            "azure-account-key-secret-canary",
            "azure-data-account-key-secret-canary",
            "stripe-api-key-secret-canary",
            "stripe-signing-secret-canary",
            "microsoft-secret-canary",
            "ldap-password-secret-canary",
            "intercom-secret-canary",
            "slack-secret-canary"
        ];
    }

    private static IEnumerable<object?> EnumerateScalarValues(LogEventPropertyValue value)
    {
        switch (value)
        {
            case ScalarValue scalar:
                yield return scalar.Value;
                break;
            case StructureValue structure:
                foreach (var child in structure.Properties.SelectMany(property => EnumerateScalarValues(property.Value)))
                    yield return child;
                break;
            case SequenceValue sequence:
                foreach (var child in sequence.Elements.SelectMany(EnumerateScalarValues))
                    yield return child;
                break;
            case DictionaryValue dictionary:
                foreach (var child in dictionary.Elements.SelectMany(pair => EnumerateScalarValues(pair.Value)))
                    yield return child;
                break;
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class TraversalTrackingValue
    {
        public int ValueAccessCount { get; private set; }

        public string Value
        {
            get
            {
                ValueAccessCount++;
                return "visible";
            }
        }
    }
}
