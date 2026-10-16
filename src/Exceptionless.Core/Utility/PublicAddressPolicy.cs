using System.Net;
using System.Net.Sockets;

namespace Exceptionless.Core.Utility;

public static class PublicAddressPolicy
{
    private static readonly IPNetwork GlobalIpv6 = IPNetwork.Parse("2000::/3");

    // Special-use, private, documentation, and transition ranges must not be outbound destinations.
    private static readonly IPNetwork[] BlockedIpv4 =
    [
        IPNetwork.Parse("0.0.0.0/8"), IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"), IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"), IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.88.99.0/24"), IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"), IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"), IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4")
    ];

    private static readonly IPNetwork[] BlockedIpv6 =
    [
        IPNetwork.Parse("2001::/23"), IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"), IPNetwork.Parse("3fff::/20")
    ];

    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => !BlockedIpv4.Any(network => network.Contains(address)),
            AddressFamily.InterNetworkV6 => GlobalIpv6.Contains(address) && !BlockedIpv6.Any(network => network.Contains(address)),
            _ => false
        };
    }
}
