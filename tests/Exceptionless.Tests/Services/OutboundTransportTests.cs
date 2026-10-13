using System.Net;
using Exceptionless.Core.Services;
using Exceptionless.Core.Services.SourceMaps;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OutboundTransportTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData(nameof(IOAuthClientMetadataService), DecompressionMethods.None)]
    [InlineData(SourceMapService.HttpClientName, DecompressionMethods.All)]
    [InlineData(SourceMapService.GeneratedFileHttpClientName, DecompressionMethods.None)]
    public void CreateHandler_OutboundClientRegistrations_UsesRestrictedTransportSettings(string clientName, DecompressionMethods expectedDecompression)
    {
        // Arrange
        var factory = GetService<IHttpMessageHandlerFactory>();

        // Act: inspect the actual registrations without making an outbound request.
        HttpMessageHandler handler = factory.CreateHandler(clientName);
        while (handler is DelegatingHandler delegatingHandler)
            handler = Assert.IsAssignableFrom<HttpMessageHandler>(delegatingHandler.InnerHandler);

        // Assert
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(sockets.AllowAutoRedirect);
        Assert.False(sockets.UseCookies);
        Assert.False(sockets.UseProxy);
        Assert.NotNull(sockets.ConnectCallback);
        Assert.Equal(expectedDecompression, sockets.AutomaticDecompression);
    }
}
