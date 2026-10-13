using System.Net.Sockets;
using Exceptionless.Core.Utility;
using Foundatio.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class OutboundSocketCleanupTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public void Dispose_SocketDisposalThrows_LogsFailureWithoutThrowing()
    {
        // Arrange
        var exception = new IOException("Socket cleanup failed");
        var socket = new Mock<Socket>(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { CallBase = true };
        using var handle = socket.Object.SafeHandle;
        socket.Protected().Setup("Dispose", ItExpr.Is<bool>(disposing => disposing)).Throws(exception);

        // Act
        OutboundSocketCleanup.Dispose(socket.Object, _logger);

        // Assert
        socket.Protected().Verify("Dispose", Times.Once(), ItExpr.Is<bool>(disposing => disposing));
        var entry = Assert.Single(Log.LogEntries, entry => entry.LogLevel == LogLevel.Warning);
        Assert.Same(exception, entry.Exception);
        Assert.Equal("Unable to dispose failed outbound socket connection: Socket cleanup failed", entry.Message);
    }
}
