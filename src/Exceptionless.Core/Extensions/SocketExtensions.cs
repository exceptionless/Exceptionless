using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Extensions;

internal static class SocketExtensions
{
    internal static void DisposeFailedConnection(this Socket socket, ILogger logger)
    {
        try
        {
            socket.Dispose();
        }
        catch (Exception ex)
        {
            // Cleanup must not replace the connection failure or cancellation being handled.
            logger.LogWarning(ex, "Unable to dispose a failed outbound socket connection.");
        }
    }
}
