using System.Net.Sockets;
using Exceptionless.Core.Extensions;
using Foundatio.Xunit;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class SocketExtensionsTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public void DisposeFailedConnection_DisposalThrows_LogsFailureWithoutThrowing()
    {
        // Arrange
        var exception = new IOException("Socket cleanup failed.");
        var socket = new ThrowingDisposeSocket(exception);
        var logger = Log.CreateLogger<SocketExtensionsTests>();

        // Act
        socket.DisposeFailedConnection(logger);

        // Assert
        Assert.True(socket.SafeHandle.IsClosed);
        var entry = Assert.Single(Log.LogEntries, entry => entry.LogLevel == LogLevel.Warning);
        Assert.Same(exception, entry.Exception);
    }

    private sealed class ThrowingDisposeSocket(Exception exception) : Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                throw exception;
        }
    }
}
