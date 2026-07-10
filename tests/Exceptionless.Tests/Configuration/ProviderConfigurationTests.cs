using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public class ProviderConfigurationTests
{
    [Fact]
    public void Resolve_WithArbitraryProviderUri_PreservesRawConnectionString()
    {
        const string providerConnectionString = "custom+ssl://user:pass@provider.example.com/resource?timeout=30";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Component"] = "provider=custom",
            ["ConnectionStrings:Custom"] = providerConnectionString
        });

        var options = ProviderConfigurationResolver.Resolve(configuration, "Component");

        Assert.Equal("custom", options.Provider);
        Assert.Equal(providerConnectionString, options.ConnectionString);
    }

    [Theory]
    [InlineData("amqp://guest:guest@localhost:5672/%2F")]
    [InlineData("amqps://user:p%40ss@rabbit.example.com:5671/team%2Fprod?heartbeat=30&connection_timeout=10000")]
    public void ReadFromConfiguration_WithNamedRabbitMqUri_PreservesRawConnectionString(string rabbitMqConnectionString)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = "provider=RaBbItMq",
            ["ConnectionStrings:RABBITMQ"] = rabbitMqConnectionString
        });

        var options = MessageBusOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("rabbitmq", options.Provider);
        Assert.Equal(rabbitMqConnectionString, options.ConnectionString);
        Assert.Equal(rabbitMqConnectionString, options.Data["server"]);
    }

    [Theory]
    [InlineData("provider=rabbitmq;amqp://localhost/%2F", "amqp://localhost/%2F")]
    [InlineData("PROVIDER=RABBITMQ;\"amqps://user:pass@localhost:5671/%2F?heartbeat=30\"", "amqps://user:pass@localhost:5671/%2F?heartbeat=30")]
    public void ReadFromConfiguration_WithInlineRabbitMqUri_PreservesRawConnectionString(string configuredValue, string expectedConnectionString)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = configuredValue
        });

        var options = MessageBusOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("rabbitmq", options.Provider);
        Assert.Equal(expectedConnectionString, options.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithQuotedNamedRabbitMqUri_RemovesWrappingQuotes()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = "provider=rabbitmq",
            ["ConnectionStrings:rabbitmq"] = "'amqp://localhost/%2F'"
        });

        var options = MessageBusOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("amqp://localhost/%2F", options.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithDocumentedRedisSettings_PreservesExistingConfiguration()
    {
        const string redisConnectionString = "redis,abortConnect=false";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Cache"] = "provider=redis",
            ["ConnectionStrings:MessageBus"] = "provider=redis",
            ["ConnectionStrings:Queue"] = "provider=redis",
            ["ConnectionStrings:Redis"] = redisConnectionString
        });
        var appOptions = CreateAppOptions();

        var cache = CacheOptions.ReadFromConfiguration(configuration, appOptions);
        var messageBus = MessageBusOptions.ReadFromConfiguration(configuration, appOptions);
        var queue = QueueOptions.ReadFromConfiguration(configuration, appOptions);

        Assert.Equal(redisConnectionString, cache.ConnectionString);
        Assert.Equal(redisConnectionString, messageBus.ConnectionString);
        Assert.Equal(redisConnectionString, queue.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithInlineRedisSettings_PreservesKeyValueConnectionString()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = "provider=redis;server=localhost,abortConnect=false"
        });

        var options = MessageBusOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("redis", options.Provider);
        Assert.Equal("server=localhost,abortConnect=false", options.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithoutCacheOrMessageBusSelectors_UsesRawRedisConnectionString()
    {
        const string redisConnectionString = "redis,abortConnect=false";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = redisConnectionString
        });
        var appOptions = CreateAppOptions();

        var cache = CacheOptions.ReadFromConfiguration(configuration, appOptions);
        var messageBus = MessageBusOptions.ReadFromConfiguration(configuration, appOptions);

        Assert.Equal("redis", cache.Provider);
        Assert.Equal(redisConnectionString, cache.ConnectionString);
        Assert.Equal("redis", messageBus.Provider);
        Assert.Equal(redisConnectionString, messageBus.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithoutQueueSelector_PrefersAzureQueuesOverRedis()
    {
        const string azureConnectionString = "UseDevelopmentStorage=true";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AzureQueues"] = azureConnectionString,
            ["ConnectionStrings:Redis"] = "redis,abortConnect=false"
        });

        var options = QueueOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("azurestorage", options.Provider);
        Assert.Equal(azureConnectionString, options.ConnectionString);
        Assert.Equal("true", options.Data["UseDevelopmentStorage"]);
    }

    [Fact]
    public void ReadFromConfiguration_WithoutStorageSelector_UsesAzureStorage()
    {
        const string azureConnectionString = "UseDevelopmentStorage=true";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AzureStorage"] = azureConnectionString
        });

        var options = StorageOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("azurestorage", options.Provider);
        Assert.Equal(azureConnectionString, options.ConnectionString);
        Assert.Equal("true", options.Data["UseDevelopmentStorage"]);
    }

    [Fact]
    public void ReadFromConfiguration_WithS3ProviderSettings_MergesNamedMetadataOverInlineMetadata()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Storage"] = "provider=s3;bucket=inline-bucket;region=us-east-1",
            ["ConnectionStrings:S3"] = "accesskey=test-access;secretkey=test-secret;bucket=named-bucket"
        });

        var options = StorageOptions.ReadFromConfiguration(configuration, CreateAppOptions());

        Assert.Equal("s3", options.Provider);
        Assert.Equal("named-bucket", options.Data["bucket"]);
        Assert.Equal("us-east-1", options.Data["region"]);
        Assert.Equal("test-access", options.Data["accesskey"]);
        Assert.DoesNotContain("provider=", options.ConnectionString);
    }

    [Theory]
    [InlineData("server=localhost")]
    [InlineData("provider=rabbitmq;not-an-absolute-uri")]
    public void ReadFromConfiguration_WithMalformedSelector_ThrowsClearError(string configuredValue)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = configuredValue
        });

        var exception = Assert.Throws<InvalidOperationException>(() => MessageBusOptions.ReadFromConfiguration(configuration, CreateAppOptions()));

        Assert.Contains("ConnectionStrings:MessageBus", exception.Message);
    }

    private static AppOptions CreateAppOptions()
    {
        return new AppOptions
        {
            AppMode = AppMode.Production,
            AppScope = "prod"
        };
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
