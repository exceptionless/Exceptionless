using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public class ProviderConfigurationTests
{
    [Theory]
    [InlineData("redis:6379,abortConnect=false")]
    [InlineData("redis,abortConnect=false")]
    public void ReadFromConfiguration_ExistingHelmAndDockerConfiguration_PreservesExplicitProviders(string redisConnectionString)
    {
        IConfiguration configuration = CreateConfiguration(new()
        {
            ["ConnectionStrings:Redis"] = redisConnectionString,
            ["ConnectionStrings:Cache"] = "provider=redis;",
            ["ConnectionStrings:MessageBus"] = "provider=redis;",
            ["ConnectionStrings:Queue"] = "provider=redis;",
            ["ConnectionStrings:Storage"] = "provider=folder;path=/app/storage"
        });

        AppOptions options = ReadOptionsFromConfiguration(configuration);

        Assert.Equal("redis", options.CacheOptions.Provider);
        Assert.Equal("redis", options.MessageBusOptions.Provider);
        Assert.Equal("redis", options.QueueOptions.Provider);
        Assert.Equal("folder", options.StorageOptions.Provider);
        Assert.Equal("/app/storage", options.StorageOptions.Data["path"]);
    }

    [Theory]
    [InlineData("Cache", "Redis", "redis:6379", "redis")]
    [InlineData("MessageBus", "RabbitMQ", "amqp://rabbit/%2F", "rabbitmq")]
    [InlineData("MessageBus", "Redis", "redis:6379", "redis")]
    [InlineData("Queue", "AzureQueues", "UseDevelopmentStorage=true", "azurestorage")]
    [InlineData("Queue", "SQS", "region=us-east-2", "sqs")]
    [InlineData("Queue", "Redis", "redis:6379", "redis")]
    [InlineData("Storage", "AzureStorage", "UseDevelopmentStorage=true", "azurestorage")]
    [InlineData("Storage", "S3", "bucket=events", "s3")]
    [InlineData("Storage", "Aliyun", "bucket=events", "aliyun")]
    [InlineData("Storage", "Folder", "path=/app/storage", "folder")]
    public void Resolve_SingleCompatibleTechnology_InfersProvider(
        string roleName,
        string connectionStringName,
        string connectionString,
        string expectedProvider)
    {
        IConfiguration configuration = CreateConfiguration(new()
        {
            [$"ConnectionStrings:{connectionStringName}"] = connectionString
        });

        ProviderRole role = Enum.Parse<ProviderRole>(roleName);
        ProviderConfiguration resolved = ProviderConfigurationResolver.Resolve(configuration, role);

        Assert.Equal(expectedProvider, resolved.Provider);
    }

    [Fact]
    public void ReadFromConfiguration_AspireConfiguration_InfersCompatibleProviders()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["ConnectionStrings:AzureStorage"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:AzureQueues"] = "UseDevelopmentStorage=true"
        });

        Assert.Equal("redis", options.CacheOptions.Provider);
        Assert.Equal("redis", options.MessageBusOptions.Provider);
        Assert.Equal("azurestorage", options.QueueOptions.Provider);
        Assert.Equal("azurestorage", options.StorageOptions.Provider);
        Assert.Equal("localhost:6379", options.CacheOptions.ConnectionString);
        Assert.Equal("localhost:6379", options.MessageBusOptions.ConnectionString);
        Assert.Equal("UseDevelopmentStorage=true", options.QueueOptions.ConnectionString);
        Assert.Equal("UseDevelopmentStorage=true", options.StorageOptions.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_AllAutomaticCandidates_UsesFixedRolePriorities()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Redis"] = "redis:6379",
            ["ConnectionStrings:RabbitMQ"] = "amqps://rabbit.example.test/%2F",
            ["ConnectionStrings:AzureQueues"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:SQS"] = "region=us-east-2",
            ["ConnectionStrings:AzureStorage"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:S3"] = "bucket=events",
            ["ConnectionStrings:Aliyun"] = "bucket=events"
        });

        Assert.Equal("redis", options.CacheOptions.Provider);
        Assert.Equal("rabbitmq", options.MessageBusOptions.Provider);
        Assert.Equal("azurestorage", options.QueueOptions.Provider);
        Assert.Equal("azurestorage", options.StorageOptions.Provider);
    }

    [Fact]
    public void ReadFromConfiguration_ExplicitRoles_OverrideAutomaticPriority()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Redis"] = "redis:6379",
            ["ConnectionStrings:RabbitMQ"] = "amqp://rabbit.example.test/%2F",
            ["ConnectionStrings:AzureQueues"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:MessageBus"] = "provider=redis",
            ["ConnectionStrings:Queue"] = "provider=redis",
            ["ConnectionStrings:Storage"] = "provider=folder;path=/data/events"
        });

        Assert.Equal("redis", options.MessageBusOptions.Provider);
        Assert.Equal("redis", options.QueueOptions.Provider);
        Assert.Equal("folder", options.StorageOptions.Provider);
    }

    [Fact]
    public void ReadFromConfiguration_LegacyRoleData_OverridesStructuredSharedProviderData()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:S3"] = "bucket=shared;region=us-east-1",
            ["ConnectionStrings:Storage"] = "provider=s3;bucket=events"
        });

        Assert.Equal("events", options.StorageOptions.Data["bucket"]);
        Assert.Equal("us-east-1", options.StorageOptions.Data["region"]);
    }

    [Fact]
    public void ReadFromConfiguration_OpaqueTechnologyWithLegacyRoleOptions_Throws()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "redis:6379,abortConnect=false",
            ["ConnectionStrings:Cache"] = "provider=redis;ssl=true"
        };

        Assert.Throws<InvalidOperationException>(() => ReadOptions(values));
    }

    [Fact]
    public void ReadFromConfiguration_TechnologyConnectionWithMismatchedProvider_Throws()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "provider=unknown;server=redis:6379"
        };

        Assert.Throws<InvalidOperationException>(() => ReadOptions(values));
    }

    [Theory]
    [InlineData("Queue", "provider=azurestorage;https://queue.example.test")]
    [InlineData("Queue", "provider=sqs;https://sqs.example.test")]
    [InlineData("Storage", "provider=azurestorage;https://storage.example.test")]
    [InlineData("Storage", "provider=s3;https://s3.example.test")]
    public void Resolve_StructuredProviderWithOpaqueInlineValue_Throws(string roleName, string selector)
    {
        IConfiguration configuration = CreateConfiguration(new()
        {
            [$"ConnectionStrings:{roleName}"] = selector
        });

        ProviderRole role = Enum.Parse<ProviderRole>(roleName);

        Assert.Throws<InvalidOperationException>(() => ProviderConfigurationResolver.Resolve(configuration, role));
    }

    [Fact]
    public void Resolve_RedisSelectedForStorage_Throws()
    {
        IConfiguration configuration = CreateConfiguration(new()
        {
            ["ConnectionStrings:Storage"] = "provider=redis;redis:6379"
        });

        Assert.Throws<InvalidOperationException>(() => ProviderConfigurationResolver.Resolve(configuration, ProviderRole.Storage));
    }

    [Fact]
    public void ReadFromConfiguration_RedisOnly_NeverSelectsStorage()
    {
        AppOptions options = ReadOptions(new() { ["ConnectionStrings:Redis"] = "redis:6379" });

        Assert.Equal("redis", options.CacheOptions.Provider);
        Assert.Equal("redis", options.MessageBusOptions.Provider);
        Assert.Equal("redis", options.QueueOptions.Provider);
        Assert.Equal("local", options.StorageOptions.Provider);
        Assert.True(options.UsesRedis());
    }

    [Fact]
    public void ReadFromConfiguration_RedisQueueOnly_EnablesRedisTelemetry()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Cache"] = "local",
            ["ConnectionStrings:MessageBus"] = "local",
            ["ConnectionStrings:Queue"] = "provider=redis;queue:6379",
            ["ConnectionStrings:Storage"] = "local"
        });

        Assert.True(options.UsesRedis());
    }

    [Fact]
    public void ReadFromConfiguration_Local_PreventsInference()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Redis"] = "redis:6379",
            ["ConnectionStrings:RabbitMQ"] = "amqp://rabbit.example.test/%2F",
            ["ConnectionStrings:AzureQueues"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:AzureStorage"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:Cache"] = "local",
            ["ConnectionStrings:MessageBus"] = "provider=LOCAL",
            ["ConnectionStrings:Queue"] = "local",
            ["ConnectionStrings:Storage"] = "local"
        });

        Assert.Equal("local", options.CacheOptions.Provider);
        Assert.Equal("local", options.MessageBusOptions.Provider);
        Assert.Equal("local", options.QueueOptions.Provider);
        Assert.Equal("local", options.StorageOptions.Provider);
        Assert.False(options.UsesRedis());
    }

    [Fact]
    public void ReadFromConfiguration_BlankRoleValue_CountsAsAbsent()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Redis"] = "redis:6379",
            ["ConnectionStrings:Cache"] = "   "
        });

        Assert.Equal("redis", options.CacheOptions.Provider);
    }

    [Theory]
    [InlineData("provider=unknown")]
    [InlineData("provider=redis")]
    [InlineData("Redis")]
    [InlineData("Redis;ssl=true")]
    [InlineData("redis:6379")]
    [InlineData("provider=local;server=redis:6379")]
    public void ReadFromConfiguration_InvalidExplicitCache_Throws(string selector)
    {
        var values = new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = selector };

        Assert.Throws<InvalidOperationException>(() => ReadOptions(values));
    }

    [Theory]
    [InlineData("provider=rabbitmq;amqp://localhost/%2F", "amqp://localhost/%2F")]
    [InlineData("provider=rabbitmq;\"amqps://user:p%40ss@rabbit.example.test:5671/team%2Fprod?heartbeat=30\"", "amqps://user:p%40ss@rabbit.example.test:5671/team%2Fprod?heartbeat=30")]
    [InlineData("provider=rabbitmq;server=\"amqps://rabbit.example.test/%2F\"", "amqps://rabbit.example.test/%2F")]
    public void ReadFromConfiguration_LegacyAndInlineRabbitMq_PreservesRawUri(string selector, string expected)
    {
        AppOptions options = ReadOptions(new() { ["ConnectionStrings:MessageBus"] = selector });

        Assert.Equal("rabbitmq", options.MessageBusOptions.Provider);
        Assert.Equal(expected, options.MessageBusOptions.ConnectionString);
        Assert.Equal(expected, options.MessageBusOptions.Data["server"]);
    }

    [Fact]
    public void ReadFromConfiguration_NamedRabbitMq_PreservesRawUri()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:MessageBus"] = "provider=RaBbItMq",
            ["ConnectionStrings:RabbitMQ"] = "'amqp://rabbit.example.test/team%2Fprod'"
        });

        Assert.Equal("amqp://rabbit.example.test/team%2Fprod", options.MessageBusOptions.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_InlineOpaqueRedis_PreservesRawConnectionString()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Cache"] = "provider=redis;redis:6379,password=p%40ss,abortConnect=false"
        });

        Assert.Equal("redis:6379,password=p%40ss,abortConnect=false", options.CacheOptions.ConnectionString);
    }

    [Theory]
    [InlineData("ssl=true,redis:6380")]
    [InlineData("password=secret,redis:6379")]
    [InlineData("serviceName=primary,redis:26379")]
    public void ReadFromConfiguration_OptionFirstRedis_PreservesNativeConnectionString(string connectionString)
    {
        AppOptions options = ReadOptions(new() { ["ConnectionStrings:Redis"] = connectionString });

        Assert.Equal(connectionString, options.CacheOptions.ConnectionString);
        Assert.Equal(connectionString, options.MessageBusOptions.ConnectionString);
        Assert.Equal(connectionString, options.QueueOptions.ConnectionString);
        Assert.Equal(connectionString, options.CacheOptions.Data["server"]);
        Assert.Single(ConfigurationOptions.Parse(connectionString).EndPoints);
    }

    [Theory]
    [InlineData("provider=redis;ssl=true,redis:6380", "ssl=true,redis:6380")]
    [InlineData("provider=redis;server=redis:6379,abortConnect=false", "redis:6379,abortConnect=false")]
    public void ReadFromConfiguration_LegacyRedisFullOverride_ProducesNativeConnectionString(
        string selector,
        string expectedConnectionString)
    {
        AppOptions options = ReadOptions(new() { ["ConnectionStrings:Cache"] = selector });

        Assert.Equal(expectedConnectionString, options.CacheOptions.ConnectionString);
        Assert.Single(ConfigurationOptions.Parse(expectedConnectionString).EndPoints);
    }

    [Fact]
    public void ReadFromConfiguration_LegacyRedisPartialOverlay_Throws()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "redis:6379,abortConnect=false",
            ["ConnectionStrings:Cache"] = "provider=redis;server=cache:6380;ssl=true"
        };

        Assert.Throws<InvalidOperationException>(() => ReadOptions(values));
    }

    [Fact]
    public void ReadFromConfiguration_StructuredTechnologyProviderMetadata_IsRemoved()
    {
        AppOptions options = ReadOptions(new()
        {
            ["ConnectionStrings:Storage"] = "provider=s3",
            ["ConnectionStrings:S3"] = "provider=s3;bucket=events;region=us-east-2"
        });

        Assert.Equal("bucket=events;region=us-east-2", options.StorageOptions.ConnectionString);
        Assert.DoesNotContain("provider", options.StorageOptions.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadFromConfiguration_FolderSelectorWithoutPath_PreservesLegacyDefault()
    {
        AppOptions options = ReadOptions(new() { ["ConnectionStrings:Storage"] = "provider=folder" });

        Assert.Equal("folder", options.StorageOptions.Provider);
        Assert.Null(options.StorageOptions.ConnectionString);
    }

    private static AppOptions ReadOptions(Dictionary<string, string?> values) => ReadOptionsFromConfiguration(CreateConfiguration(values));

    private static AppOptions ReadOptionsFromConfiguration(IConfiguration configuration)
    {
        var values = new Dictionary<string, string?> { ["BaseURL"] = "http://localhost" };
        IConfiguration combined = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .AddConfiguration(configuration)
            .Build();
        return AppOptions.ReadFromConfiguration(combined);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
