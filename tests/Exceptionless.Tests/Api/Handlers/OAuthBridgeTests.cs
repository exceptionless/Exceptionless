using Exceptionless.Core;
using Exceptionless.Web.Api.Handlers;
using Exceptionless.Web.Api.Messages;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Exceptionless.Tests.Api.Handlers;

public sealed class OAuthBridgeTests
{
    [Theory]
    [InlineData("https://app.localhost:7131")]
    [InlineData("https://app.localhost:7131/next/#!")]
    public async Task RedirectToAuthorizeBridge_SeparateApiHost_UsesConfiguredAppOriginAndPreservesQuery(string baseUrl)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.localhost:7111");
        context.Request.QueryString = new QueryString("?client_id=test&state=a%2Bb%26c&redirect_uri=http%3A%2F%2Flocalhost%2Fcallback");
        var handler = new OAuthHandler(null!, new AppOptions { BaseURL = baseUrl }, null!, TimeProvider.System, new HttpContextAccessor { HttpContext = context });

        var result = Assert.IsType<RedirectHttpResult>(await handler.Handle(new RedirectToAuthorizeBridge()));

        Assert.Equal("https://app.localhost:7131/oauth/authorize?client_id=test&state=a%2Bb%26c&redirect_uri=http%3A%2F%2Flocalhost%2Fcallback", result.Url);
        Assert.False(result.Permanent);
    }
}
