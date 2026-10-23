using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Utility;

internal static class OutboundSocketCleanup
{
    /// <summary>
    /// Disposes a socket after a failed connection without replacing the original failure or cancellation.
    /// </summary>
    internal static void Dispose(Socket socket, ILogger logger)
    {
        try
        {
            socket.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to dispose failed outbound socket connection: {Message}", ex.Message);
        }
    }
}
