using System.Net;
using System.Net.Sockets;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Services;

public sealed class WebHookDestinationPolicy(AppOptions options)
{
    public const string HttpClientName = "WebHooks";
    public static readonly HttpRequestOptionsKey<bool> DeliveryRequest = new("Exceptionless.WebHookDelivery");

    public bool IsAllowed(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        return PublicAddressPolicy.IsPublic(address) || options.WebHookOptions.AllowedPrivateNetworks.Any(network => network.Contains(address));
    }

    public static bool TryCreateUri(string? value, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !String.IsNullOrEmpty(uri.Host) && String.IsNullOrEmpty(uri.UserInfo) && String.IsNullOrEmpty(uri.Fragment);
    }

    public bool IsValidDestination(string? value)
        => TryCreateUri(value, out var uri) && (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)
            ? IsAllowed(address)
            : !uri.IsLoopback || IsAllowed(IPAddress.Loopback) || IsAllowed(IPAddress.IPv6Loopback));

    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? lastException = null;
        foreach (var address in addresses.Where(IsAllowed))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                lastException = ex;
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("Web hook destination has no reachable allowed address.", lastException);
    }

    public static string GetLoggingDestination(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            : "invalid";
}
