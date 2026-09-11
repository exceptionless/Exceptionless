using System.Net;
using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Redis;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public class RedisConnectionRegistryTests
{
    [Fact]
    public void GetConnection_EqualStrings_CreatesOneConnection()
    {
        int creationCount = 0;
        using var registry = CreateRegistry(_ =>
        {
            creationCount++;
            return CreateConnection();
        });

        IConnectionMultiplexer first = registry.GetConnection("redis:6379,password=secret");
        IConnectionMultiplexer second = registry.GetConnection("redis:6379,password=secret");

        Assert.Same(first, second);
        Assert.Equal(1, creationCount);
    }

    [Fact]
    public void GetConnection_DifferentRoleStrings_RemainIsolated()
    {
        using var registry = CreateRegistry(_ => CreateConnection());

        IConnectionMultiplexer cache = registry.GetConnection("cache:6379");
        IConnectionMultiplexer messageBus = registry.GetConnection("message-bus:6379");
        IConnectionMultiplexer queue = registry.GetConnection("queue:6379");

        Assert.NotSame(cache, messageBus);
        Assert.NotSame(messageBus, queue);
        Assert.NotSame(cache, queue);
    }

    [Fact]
    public void Dispose_RegistryOwnedConnections_DisposesEachConnectionOnce()
    {
        var proxies = new List<MultiplexerProxy>();
        var registry = CreateRegistry(_ => CreateConnection(proxies));
        registry.GetConnection("message-bus:6379");
        registry.GetConnection("message-bus:6379");
        registry.GetConnection("queue:6379");

        registry.Dispose();
        registry.Dispose();

        Assert.Equal(2, proxies.Count);
        Assert.All(proxies, proxy => Assert.Equal(1, proxy.DisposeCount));
    }

    [Fact]
    public void Dispose_CacheCompatibilityConnection_IsDisposedOnceByServiceProviderOwner()
    {
        var proxies = new List<MultiplexerProxy>();
        var registry = CreateRegistry(_ => CreateConnection(proxies));
        IConnectionMultiplexer cache = registry.GetCacheConnection("cache:6379");

        cache.Dispose();
        registry.Dispose();

        Assert.Single(proxies);
        Assert.Equal(1, proxies[0].DisposeCount);
    }

    [Fact]
    public void RegisterServices_RedisQueueWithLocalCache_RegistersQueueWithoutCacheMultiplexer()
    {
        AppOptions options = CreateOptions(new()
        {
            ["ConnectionStrings:Cache"] = "local",
            ["ConnectionStrings:MessageBus"] = "local",
            ["ConnectionStrings:Queue"] = "provider=redis;queue:6379",
            ["ConnectionStrings:Storage"] = "local"
        });
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        Exceptionless.Insulation.Bootstrapper.RegisterServices(services, options, runMaintenanceTasks: false);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RedisConnectionRegistry));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IQueue<EventPost>));
    }

    [Theory]
    [InlineData("server=redis:6379")]
    [InlineData("redis:6379;abortConnect=false")]
    [InlineData("ssl=true")]
    [InlineData("redis:6379;password=redis-password-secret-canary")]
    public void RegisterServices_InvalidRedisConnectionString_FailsBeforeServiceResolution(string connectionString)
    {
        AppOptions options = CreateOptions(new()
        {
            ["ConnectionStrings:Redis"] = connectionString,
            ["ConnectionStrings:Storage"] = "local"
        });
        var services = new ServiceCollection();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            Exceptionless.Insulation.Bootstrapper.RegisterServices(services, options, runMaintenanceTasks: false));

        Assert.Contains("Redis connection string selected for Cache is invalid", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(connectionString, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("redis-password-secret-canary", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("redis:6379,abortConnect=false")]
    [InlineData("ssl=true,redis:6380")]
    [InlineData("password=secret,redis:6379")]
    [InlineData("serviceName=primary,redis:26379")]
    public void RegisterServices_ValidNativeRedisConnectionString_PassesStartupValidation(string connectionString)
    {
        AppOptions options = CreateOptions(new()
        {
            ["ConnectionStrings:Redis"] = connectionString,
            ["ConnectionStrings:Storage"] = "local"
        });
        var services = new ServiceCollection();

        Exceptionless.Insulation.Bootstrapper.RegisterServices(services, options, runMaintenanceTasks: false);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RedisConnectionRegistry));
    }

    [Fact]
    public void RegisterServices_DifferentRedisRoleStrings_RequestIsolatedEndpoints()
    {
        AppOptions options = CreateOptions(new()
        {
            ["ConnectionStrings:Cache"] = "provider=redis;cache:6379",
            ["ConnectionStrings:MessageBus"] = "provider=redis;message-bus:6379",
            ["ConnectionStrings:Queue"] = "provider=redis;queue:6379",
            ["ConnectionStrings:Storage"] = "local"
        });
        var requestedEndpoints = new List<string>();
        var registry = CreateRegistry(endpoint =>
        {
            requestedEndpoints.Add(endpoint);
            return CreateConnection(configuration: endpoint);
        });
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        Exceptionless.Core.Bootstrapper.RegisterServices(services, options);
        Exceptionless.Insulation.Bootstrapper.RegisterServices(services, options, runMaintenanceTasks: false);
        services.RemoveAll<RedisConnectionRegistry>();
        services.AddSingleton(registry);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IConnectionMapping>();
        serviceProvider.GetRequiredService<IMessageBus>();
        serviceProvider.GetRequiredService<IQueue<EventPost>>();

        Assert.Equal(["cache:6379", "message-bus:6379", "queue:6379"], requestedEndpoints);
    }

    private static RedisConnectionRegistry CreateRegistry(Func<string, IConnectionMultiplexer> factory)
    {
        return new RedisConnectionRegistry(
            NullLoggerFactory.Instance,
            (connectionString, _) => factory(connectionString));
    }

    private static IConnectionMultiplexer CreateConnection(
        List<MultiplexerProxy>? proxies = null,
        string configuration = "localhost:6379")
    {
        IConnectionMultiplexer connection = DispatchProxy.Create<IConnectionMultiplexer, MultiplexerProxy>();
        var proxy = (MultiplexerProxy)(object)connection;
        proxy.Connection = connection;
        proxy.Configuration = configuration;
        proxies?.Add(proxy);
        return connection;
    }

    private static AppOptions CreateOptions(Dictionary<string, string?> values)
    {
        values["BaseURL"] = "http://localhost";
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return AppOptions.ReadFromConfiguration(configuration);
    }

    public class MultiplexerProxy : DispatchProxy
    {
        public IConnectionMultiplexer Connection { get; set; } = null!;
        public string Configuration { get; set; } = null!;
        public int DisposeCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IDisposable.Dispose))
                DisposeCount++;

            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetSubscriber))
            {
                ISubscriber subscriber = DispatchProxy.Create<ISubscriber, SubscriberProxy>();
                ((SubscriberProxy)(object)subscriber).Connection = Connection;
                return subscriber;
            }

            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase))
                return DispatchProxy.Create<IDatabase, DefaultProxy>();

            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetEndPoints))
                return Array.Empty<EndPoint>();

            if (targetMethod?.Name == "get_Configuration")
                return Configuration;

            return targetMethod?.ReturnType == typeof(void)
                ? null
                : targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    public class SubscriberProxy : DefaultProxy
    {
        public IConnectionMultiplexer Connection { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Multiplexer")
                return Connection;

            return base.Invoke(targetMethod, args);
        }
    }

    public class DefaultProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(void))
                return null;

            if (targetMethod?.ReturnType == typeof(Task))
                return Task.CompletedTask;

            if (targetMethod?.ReturnType.IsValueType == true)
                return Activator.CreateInstance(targetMethod.ReturnType);

            return null;
        }
    }
}
