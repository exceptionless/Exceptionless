using Exceptionless.Core.Services;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OutboundTransportTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public void MetadataClient_Transport_UsesDirectPublicConnections()
    {
        var factory = GetService<IHttpMessageHandlerFactory>();
        HttpMessageHandler handler = factory.CreateHandler(nameof(IOAuthClientMetadataService));
        while (handler is DelegatingHandler delegatingHandler)
            handler = Assert.IsAssignableFrom<HttpMessageHandler>(delegatingHandler.InnerHandler);
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(sockets.AllowAutoRedirect);
        Assert.False(sockets.UseCookies);
        Assert.False(sockets.UseProxy);
        Assert.NotNull(sockets.ConnectCallback);
    }
}
