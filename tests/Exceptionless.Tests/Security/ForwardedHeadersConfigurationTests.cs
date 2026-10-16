using System.Collections;
using System.Net;
using Exceptionless.Core.Configuration;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Security;
using Foundatio.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Exceptionless.Tests.Security;

public sealed class ForwardedHeadersConfigurationTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData("192.0.2.10", "198.51.100.20", "1", null, "198.51.100.20", "http")]
    [InlineData("198.51.100.20", "198.51.100.20", "1", null, "203.0.113.40", "https")]
    [InlineData("198.51.100.20", "198.51.100.20", "0", null, "198.51.100.20", "http")]
    [InlineData("198.51.100.20", "::ffff:198.51.100.20", "1", null, "203.0.113.40", "https")]
    [InlineData("2001:db8::20", "2001:db8::20", "1", null, "203.0.113.40", "https")]
    [InlineData("198.51.100.20", "198.51.100.20", "1", "false", "198.51.100.20", "http")]
    public async Task Invoke_ForwardedAddress_RespectsConfiguredPeerAndLimit(string trustedPeer, string remoteAddress, string limit, string? enabled, string expectedAddress, string expectedScheme)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = enabled,
            ["ForwardedHeaders:KnownProxies:0"] = trustedPeer,
            ["ForwardedHeaders:ForwardLimit"] = limit
        }).Build();
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
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
    [InlineData("1", "192.0.2.10", "192.0.2.11", "http")]
    [InlineData("2", "192.0.2.10", "203.0.113.40", "https")]
    [InlineData("3", "192.0.2.10", "203.0.113.40", "https")]
    [InlineData("2", "::ffff:192.0.2.10", "203.0.113.40", "https")]
    public async Task Invoke_ForwardedChain_UsesConfiguredDepthAndStopsAtUnknownPeer(string limit, string remoteAddress, string expectedAddress, string expectedScheme)
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
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 203.0.113.40, 192.0.2.11";
        context.Request.Headers["X-Forwarded-Proto"] = "http, https, http";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.Equal(expectedAddress, context.Request.GetClientIpAddress());
        Assert.Equal(expectedScheme, context.Request.Scheme);
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

    [Theory]
    [InlineData("ForwardedHeaders:ForwardLimit", "-1")]
    [InlineData("ForwardedHeaders_Enabled", "true")]
    public void Configure_UnsafeScalarConfiguration_ThrowsWithKey(string key, string value)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [key] = value
        }).Build();

        // Act
        var exception = Record.Exception(() => ForwardedHeadersConfiguration.Configure(new ForwardedHeadersOptions(), configuration));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("KnownNetworks:0", "0.0.0.0/0")]
    [InlineData("KnownNetworks:0", "::/0")]
    [InlineData("KnownNetworks:0", "192.0.999.0/24")]
    [InlineData("KnownNetworks:0", "192.0.2.0/33")]
    [InlineData("KnownProxies:0", "proxy.example.com")]
    [InlineData("KnownProxies:0", "192.0.2.10:8080")]
    [InlineData("KnownProxies:0", "")]
    [InlineData("KnownProxies", "192.0.2.10,192.0.2.11")]
    [InlineData("KnownNetworks", "192.0.2.0/24")]
    public void Configure_InvalidPeerConfiguration_ThrowsWithKey(string key, string value)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ForwardedHeaders:{key}"] = value
        }).Build();

        // Act
        var exception = Record.Exception(() => ForwardedHeadersConfiguration.Configure(new ForwardedHeadersOptions(), configuration));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains($"ForwardedHeaders:{key}", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task Invoke_Defaults_TrustsLoopbackAndProcessesOneHop(string remoteAddress)
    {
        // Arrange
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 203.0.113.40";
        context.Request.Headers["X-Forwarded-Proto"] = "http, https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        // Act
        await middleware.Invoke(context);

        // Assert
        Assert.Equal("203.0.113.40", context.Request.GetClientIpAddress());
        Assert.Equal("https", context.Request.Scheme);
    }

    [Theory]
    [InlineData("true", ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto)]
    [InlineData("false", ForwardedHeaders.None)]
    public void Configure_PrefixedEnvironmentLists_ReadsNestedSettings(string enabled, ForwardedHeaders expectedHeaders)
    {
        // Arrange
        var provider = new CustomEnvironmentVariablesConfigurationProvider();
        using var configuration = new ConfigurationRoot([provider]);
        provider.Load(new Hashtable
        {
            ["EX_ForwardedHeaders__Enabled"] = enabled,
            ["EX_ForwardedHeaders__KnownProxies__0"] = "192.0.2.10",
            ["EX_ForwardedHeaders__KnownNetworks__0"] = "2001:db8::/64",
            ["EX_ForwardedHeaders__ForwardLimit"] = "2"
        });
        var options = new ForwardedHeadersOptions();

        // Act
        ForwardedHeadersConfiguration.Configure(options, configuration);

        // Assert
        Assert.Contains(IPAddress.Parse("192.0.2.10"), options.KnownProxies);
        Assert.Contains(System.Net.IPNetwork.Parse("2001:db8::/64"), options.KnownIPNetworks);
        Assert.Equal(2, options.ForwardLimit);
        Assert.Equal(expectedHeaders, options.ForwardedHeaders);
    }
}
