using System.Net;
using Exceptionless.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class ForwardedHeadersConfigurationTests
{
    [Theory]
    [InlineData("192.0.2.10", "1", "198.51.100.20", "http")]
    [InlineData("198.51.100.20", "1", "203.0.113.40", "https")]
    [InlineData("198.51.100.20", "0", "198.51.100.20", "http")]
    public async Task Invoke_ForwardedAddress_RespectsConfiguredPeerAndLimit(string trustedPeer, string limit, string expectedAddress, string expectedScheme)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = trustedPeer,
            ["ForwardedHeaders:ForwardLimit"] = limit
        }).Build();
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.40";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.Equal(expectedAddress, context.Request.GetClientIpAddress());
        Assert.Equal(expectedScheme, context.Request.Scheme);
    }

    [Theory]
    [InlineData("1", "192.0.2.11")]
    [InlineData("2", "203.0.113.40")]
    [InlineData("3", "203.0.113.40")]
    public async Task Invoke_ForwardedChain_UsesConfiguredDepthAndStopsAtUnknownPeer(string limit, string expectedAddress)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownNetworks:0"] = "192.0.2.0/24",
            ["ForwardedHeaders:ForwardLimit"] = limit
        }).Build();
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 203.0.113.40, 192.0.2.11";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.Equal(expectedAddress, context.Request.GetClientIpAddress());
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task Invoke_DirectRequest_PreservesConnectionAddressAndScheme(string scheme)
    {
        // Arrange
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.40");
        context.Request.Scheme = scheme;
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.Equal("203.0.113.40", context.Request.GetClientIpAddress());
        Assert.Equal(scheme, context.Request.Scheme);
    }

    [Fact]
    public void Configure_NegativeForwardLimit_Throws()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:ForwardLimit"] = "-1"
        }).Build();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => ForwardedHeadersConfiguration.Configure(new ForwardedHeadersOptions(), configuration));

        // Assert
        Assert.Contains("ForwardedHeaders:ForwardLimit", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void Configure_UnboundedTrustedNetwork_Throws(string network)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownNetworks:0"] = network
        }).Build();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => ForwardedHeadersConfiguration.Configure(new ForwardedHeadersOptions(), configuration));

        // Assert
        Assert.Contains("ForwardedHeaders:KnownNetworks", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_Defaults_RetainsLoopbackTrustAndOneHopLimit()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();

        // Act
        ForwardedHeadersConfiguration.Configure(options, new ConfigurationBuilder().Build());

        // Assert
        Assert.NotEmpty(options.KnownProxies);
        Assert.NotEmpty(options.KnownIPNetworks);
        Assert.Equal(1, options.ForwardLimit);
    }
}
