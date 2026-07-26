using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public class MessageBusOptionsTests
{
    [Theory]
    [InlineData("provider=rabbitmq;amqp://localhost/%2F", "amqp://localhost/%2F")]
    [InlineData("provider=rabbitmq;\"amqp://localhost/%2F\"", "amqp://localhost/%2F")]
    [InlineData("provider=rabbitmq;'amqp://localhost/%2F'", "amqp://localhost/%2F")]
    [InlineData(
        " PROVIDER = \"RABBITMQ\" ; 'amqps://user:p%40ss@rabbit.example.com:5671/team%2Fprod?heartbeat=30&connection_timeout=10000' ",
        "amqps://user:p%40ss@rabbit.example.com:5671/team%2Fprod?heartbeat=30&connection_timeout=10000")]
    public void ReadFromConfiguration_WithInlineRabbitMqUri_PreservesRawConnectionString(string configuredConnectionString, string expectedConnectionString)
    {
        var options = ReadOptions(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = configuredConnectionString
        });

        Assert.Equal("rabbitmq", options.Provider);
        Assert.Equal(expectedConnectionString, options.ConnectionString);
        Assert.Equal(expectedConnectionString, options.Data["server"]);
    }

    [Theory]
    [InlineData("provider=rabbitmq", "amqp://localhost/%2F", "amqp://localhost/%2F")]
    [InlineData("provider=rabbitmq;", "'amqp://localhost/%2F'", "amqp://localhost/%2F")]
    [InlineData(
        "provider=RaBbItMq",
        "\"amqps://user:p%40ss@rabbit.example.com:5671/team%2Fprod?heartbeat=30\"",
        "amqps://user:p%40ss@rabbit.example.com:5671/team%2Fprod?heartbeat=30")]
    public void ReadFromConfiguration_WithNamedRabbitMqUri_PreservesRawConnectionString(string selector, string configuredConnectionString, string expectedConnectionString)
    {
        var options = ReadOptions(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = selector,
            ["ConnectionStrings:rabbitmq"] = configuredConnectionString
        });

        Assert.Equal("rabbitmq", options.Provider);
        Assert.Equal(expectedConnectionString, options.ConnectionString);
        Assert.Equal(expectedConnectionString, options.Data["server"]);
    }

    [Fact]
    public void ReadFromConfiguration_WithRedisProviderSettings_MergesLegacyKeyValueData()
    {
        var options = ReadOptions(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = "provider=redis;ssl=true",
            ["ConnectionStrings:redis"] = "server=localhost:6379;abortConnect=false"
        });

        Assert.Equal("redis", options.Provider);
        Assert.Equal("ssl=true;server=localhost:6379;abortConnect=false", options.ConnectionString);
        Assert.Equal("true", options.Data["ssl"]);
        Assert.Equal("localhost:6379", options.Data["server"]);
        Assert.Equal("false", options.Data["abortConnect"]);
    }

    [Fact]
    public void ReadFromConfiguration_WithInlineRedisConnectionString_PreservesLegacyFormatting()
    {
        var options = ReadOptions(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MessageBus"] = "provider=redis;server=localhost:6379,abortConnect=false"
        });

        Assert.Equal("redis", options.Provider);
        Assert.Equal("server=localhost:6379,abortConnect=false", options.ConnectionString);
        Assert.Equal("localhost:6379,abortConnect=false", options.Data["server"]);
    }

    private static MessageBusOptions ReadOptions(Dictionary<string, string?> values)
    {
        var configuration = CreateConfiguration(values);
        var appOptions = new AppOptions { AppScope = "production" };

        return MessageBusOptions.ReadFromConfiguration(configuration, appOptions);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
