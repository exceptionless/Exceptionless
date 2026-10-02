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
    [InlineData(false, "198.51.100.20")]
    [InlineData(true, "203.0.113.40")]
    public async Task Invoke_ForwardedAddress_OnlyUsesConfiguredPeer(bool trusted, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies:0"] = trusted ? "198.51.100.20" : "192.0.2.10"
        }).Build();
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.40";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal(expected, context.Request.GetClientIpAddress());
        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
    }

    [Fact]
    public async Task Invoke_ForwardedChain_UsesConfiguredDepthAndStopsAtUnknownPeer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownNetworks:0"] = "192.0.2.0/24",
            ["ForwardedHeaders:ForwardLimit"] = "2"
        }).Build();
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1, 203.0.113.40, 192.0.2.11";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal("203.0.113.40", context.Request.GetClientIpAddress());
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void Configure_UnboundedTrustedNetwork_Throws(string network)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownNetworks:0"] = network
        }).Build();
        Assert.Throws<InvalidOperationException>(() => ForwardedHeadersConfiguration.Configure(new ForwardedHeadersOptions(), configuration));
    }

    [Fact]
    public void Configure_Defaults_RetainsLoopbackTrustAndOneHopLimit()
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersConfiguration.Configure(options, new ConfigurationBuilder().Build());
        Assert.NotEmpty(options.KnownProxies);
        Assert.NotEmpty(options.KnownIPNetworks);
        Assert.Equal(1, options.ForwardLimit);
    }
}
