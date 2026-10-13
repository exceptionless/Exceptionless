using System.Net;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class WebHookDestinationPolicyTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData("https://example.com/path?token=value", true)]
    [InlineData("http://8.8.8.8/path", true)]
    [InlineData("http://127.0.0.1/path", false)]
    [InlineData("http://localhost/path", false)]
    [InlineData("http://[::ffff:127.0.0.1]/path", false)]
    [InlineData("http://169.254.169.254/path", false)]
    [InlineData("https://user:password@example.com/path", false)]
    [InlineData("https://example.com/path#fragment", false)]
    [InlineData("ftp://example.com/path", false)]
    public void IsValidDestination_DefaultConfiguration_UsesAllowedHttpDestinations(string url, bool allowed)
        => Assert.Equal(allowed, GetService<WebHookDestinationPolicy>().IsValidDestination(url));

    [Fact]
    public void IsAllowed_PrivateOverride_OnlyAppliesToConfiguredNetwork()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebHooks:AllowedPrivateNetworks:0"] = "10.20.30.0/24"
        }).Build();
        var options = GetService<AppOptions>();
        options.WebHookOptions = WebHookOptions.ReadFromConfiguration(configuration);
        var policy = new WebHookDestinationPolicy(options);
        Assert.True(policy.IsAllowed(IPAddress.Parse("10.20.30.1")));
        Assert.True(policy.IsAllowed(IPAddress.Parse("::ffff:10.20.30.1")));
        Assert.False(policy.IsAllowed(IPAddress.Parse("10.20.31.1")));
        Assert.False(policy.IsAllowed(IPAddress.Parse("169.254.169.254")));
    }

    [Fact]
    public async Task NamedClient_LoopbackDnsDestination_DoesNotConnect()
    {
        using var client = GetService<IHttpClientFactory>().CreateClient(WebHookDestinationPolicy.HttpClientName);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://localhost:1/private", TestCancellationToken));
    }

    [Fact]
    public void NamedClient_Transport_DisablesImplicitBehavior()
    {
        HttpMessageHandler handler = GetService<IHttpMessageHandlerFactory>().CreateHandler(WebHookDestinationPolicy.HttpClientName);
        while (handler is DelegatingHandler delegating)
            handler = Assert.IsAssignableFrom<HttpMessageHandler>(delegating.InnerHandler);
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(sockets.AllowAutoRedirect);
        Assert.False(sockets.UseProxy);
        Assert.False(sockets.UseCookies);
        Assert.Null(sockets.ActivityHeadersPropagator);
        Assert.NotNull(sockets.ConnectCallback);
    }

    [Fact]
    public void GetLoggingDestination_CapabilityUrl_OmitsCredentialsPathAndQuery()
    {
        string destination = WebHookDestinationPolicy.GetLoggingDestination("https://user:password@example.com/private-capability?secret-query=value#fragment");
        Assert.Equal("https://example.com", destination);
    }
}
