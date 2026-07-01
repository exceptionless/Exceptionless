using Exceptionless.Core;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public class MessageBusOptionsTests
{
    [Fact]
    public void ReadFromConfiguration_WithInlineRabbitMqUri_PreservesRawConnectionString()
    {
        // Arrange
        const string rabbitMqConnectionString = "amqp://localhost/%2F";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost:7110/#!",
            ["ConnectionStrings:MessageBus"] = $"provider=rabbitmq;{rabbitMqConnectionString}"
        });

        // Act
        var options = AppOptions.ReadFromConfiguration(configuration);

        // Assert
        Assert.Equal("rabbitmq", options.MessageBusOptions.Provider);
        Assert.Equal(rabbitMqConnectionString, options.MessageBusOptions.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithQuotedRabbitMqUri_PreservesRawConnectionString()
    {
        // Arrange
        const string rabbitMqConnectionString = "amqp://localhost/%2F";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost:7110/#!",
            ["ConnectionStrings:MessageBus"] = $"provider=rabbitmq;\"{rabbitMqConnectionString}\""
        });

        // Act
        var options = AppOptions.ReadFromConfiguration(configuration);

        // Assert
        Assert.Equal("rabbitmq", options.MessageBusOptions.Provider);
        Assert.Equal(rabbitMqConnectionString, options.MessageBusOptions.ConnectionString);
    }

    [Fact]
    public void ReadFromConfiguration_WithRabbitMqProviderConnectionString_PreservesRawConnectionString()
    {
        // Arrange
        const string rabbitMqConnectionString = "amqp://localhost/%2F";
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost:7110/#!",
            ["ConnectionStrings:MessageBus"] = "provider=rabbitmq",
            ["ConnectionStrings:rabbitmq"] = $"\"{rabbitMqConnectionString}\""
        });

        // Act
        var options = AppOptions.ReadFromConfiguration(configuration);

        // Assert
        Assert.Equal("rabbitmq", options.MessageBusOptions.Provider);
        Assert.Equal(rabbitMqConnectionString, options.MessageBusOptions.ConnectionString);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
